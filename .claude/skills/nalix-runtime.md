# Nalix.Runtime

## Triggers
- Adding or modifying a packet handler
- Adding or modifying middleware
- Debugging dispatch, throttling, or session behavior
- Changing handler authorization or encryption rules

---

## Rules

### Handler Shape
- Handler class: `sealed` + `[PacketController("Namespace")]`
- Handler method: **must be `static async ValueTask`** — instance methods are not resolved by `PacketDispatcherBase`
- Every method requires: `[PacketOpcode(ushort)]`, `[PacketPermission(PermissionLevel)]`, `[PacketEncryption(bool)]`
- Pre-auth guard: check `connection.Secret.IsZero` before processing — true = not yet authenticated → disconnect

### Auto-Registered Handlers
`NetworkApplicationBuilder` always registers these four — **do not register them again**:

| Handler | Opcode |
| :--- | :--- |
| `KeyExchangeHandlers` | `KEY_EXCHANGE` |
| `HandshakeHandlers` | `CLIENT_HELLO`, `CLIENT_FINISH` |
| `SessionHandlers` | `SESSION_RESUME` |
| `SystemControlHandlers` | Error, throttle, cipher, ping |

### Protocol Ordering Invariant
`KEY_EXCHANGE` → `CLIENT_HELLO` → `CLIENT_FINISH` → application opcodes

`KeyExchangeHandlers` checks `ConnectionAttributes.HandshakeEstablished` and calls `Disconnect()` if already set. Breaking this order causes a hard disconnect at runtime, not a soft error.

### Handshake Two-Stage Protocol
- **`CLIENT_HELLO`**: generate ephemeral X25519 key pair, compute two shared secrets — EE (ephemeral-ephemeral, forward secrecy) + SE (static-ephemeral, auth) — derive master secret via HKDF-Extract (no salt over both)
- **`CLIENT_FINISH`**: validate client proof, derive session key, call `SaveSessionAsync()` — **session is saved here, not at HELLO**
- Both EE and SE results must be non-zero — zero output = reject with `DECRYPTION_FAILED`
- All intermediate secrets (EE, SE, master) are `ZeroMemory`'d immediately after use — they remain GC-visible until zeroed

### Middleware Execution
Three internal lists execute in order: **Inbound → (handler) → OutboundAlways → Outbound**

- `MiddlewareStage.Inbound`: runs before the handler
- `MiddlewareStage.Outbound`: runs after the handler — skipped if handler throws
- `MiddlewareStage.Both`: registered in both Inbound and Outbound lists
- `AlwaysExecute = true` (OutboundAlways): runs after the handler **even when it throws** — use for cleanup, audit, metrics
- `[MiddlewareOrder(n)]`: lower n = earlier = runs first; security before business logic
- Middleware snapshot is immutable — execution is lock-free via volatile `PipelineSnapshot`; registration/removal requires a lock
- `continueOnError = true` logs exceptions and skips the failing middleware without crashing the pipeline

### Throttling (Token Bucket)
- 1 token = `TokenScale` (e.g. 1,000,000) micro-tokens stored as `long` — no floating-point on hot path
- Accumulator tracks remainder across refill cycles to prevent precision decay
- Two tiers: **soft throttle** (`RetryAfterMs` returned) → **hard lockout** (after `MaxSoftViolations` exceeded within window)
- If tracked endpoints > `MaxTrackedEndpoints`: new endpoints receive hard lockout immediately (memory guard)
- Cleanup job is non-reentrant; rotates shard start index to prevent starvation (BUG-25)

### PacketContext Lifecycle
State machine: `Pooled → InUse → Returned`

Do not hold a `PacketContext` reference after the handler returns — it is returned to the pool immediately on `Dispose()`, which is called by the dispatch framework.

---

## Checklists

### Add a new handler
1. `public sealed class MyHandlers` decorated with `[PacketController("Nalix.YourArea")]`
2. Each method: `public static async ValueTask HandleAsync(IPacketContext<TPacket> context)`
3. Decorate each method: `[PacketOpcode((ushort)OpCode.VALUE)]`, `[PacketPermission(PermissionLevel.X)]`, `[PacketEncryption(true/false)]`
4. If system/reserved opcode: also add `[ReservedOpcodePermitted]`
5. If requires auth: `if (context.Connection.Secret.IsZero) { context.Connection.Disconnect("..."); return; }`
6. Register: `builder.MapHandlers<MyHandlers>()`

### Standard Middleware (Built-in)
Four middleware types ship with `Nalix.Runtime` and are wired via `ConfigureDispatchOptions`:

| Class | `[MiddlewareOrder]` | Stage | Purpose |
| :--- | :--- | :--- | :--- |
| `PermissionMiddleware` | `-50` | Inbound | Permission level check — runs first |
| `ConcurrencyMiddleware` | `50` | Inbound | Per-connection concurrency cap |
| `RateLimitMiddleware` | `50` | Inbound | Token-bucket rate limiting |
| `TimeoutMiddleware` | `75` | Inbound | Handler execution timeout |

**Custom middleware order guidance:** security guards < 100, rate limiting 50–100, business logic > 500.

### Add custom middleware
1. Implement `IPacketMiddleware`
2. `[MiddlewareOrder(n)]` — see order guidance above
3. Stage annotation if not default: `[MiddlewareStage(MiddlewareStage.OutboundAlways)]` for audit/cleanup
4. Register via `ConfigureDispatchOptions` on the builder:
```csharp
builder.ConfigureDispatchOptions(opts =>
    opts.WithMiddleware(new MyMiddleware()));
```

---

## Gotchas

- **Handshake slot claim is lock-free via object reference identity**: `TryAcquireHandshakeSlot()` uses a sentinel `object` stored in connection attributes. Two concurrent handshake attempts compete via reference equality — no mutex. Do not call handshake handlers directly; they assume single active claim.

- **`lease.Retain()` before enqueue**: `PacketDispatchChannel.HandlePacket()` calls `Retain()` on the buffer lease before queuing. If you manually queue into the channel, you must also call `Retain()` — omitting it causes use-after-free when the sender releases the original lease.

- **`MemoryPacket` handlers bypass deserialization**: If a handler parameter type is `MemoryPacket`, the raw buffer is wrapped without schema decoding. Useful for passthrough proxies; dangerous if you expect typed fields.

- **Coalesced wake-up spins before blocking**: Worker loops spin 16×8 iterations before waiting on the semaphore. Under heavy load, this is normal behavior — don't interpret spin as deadlock.

- **`ZeroMemory` on handshake secrets is mandatory**: Skipping `MemorySecurity.ZeroMemory()` on EE, SE, or master secrets leaves key material in GC-reachable memory until the next collection. This is a security defect, not just a style issue.

- **Accumulator reset causes token leak**: The microtoken accumulator preserves fractional tokens across refill cycles. If you zero `MicroBalance` manually without preserving the accumulator, tokens leak on every refill tick.

- **`OutboundAlways` vs `Outbound`**: If you need guaranteed post-handler cleanup (audit log, metrics), register as `OutboundAlways`. Plain `Outbound` does not run when the handler stage throws.
