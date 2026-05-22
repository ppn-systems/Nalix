# Nalix.Hosting

## Triggers
- Bootstrapping a new Nalix server application
- Adding handlers, middleware, or custom protocols
- Configuring lifecycle (start/stop) behavior
- Modifying transport bindings or options

---

## Rules

### Startup Sequence (Fixed Order)
1. Configuration loading (INI → POCO binding)
2. `InstanceManager` service registration
3. Middleware pipeline assembly — **in the order `AddMiddleware<>()` is called**
4. Handler registration — opcode-keyed, order does not matter
5. `NetworkApplication.Activate()` — starts packet dispatch and listeners

**Middleware registration order matters. Handler registration order does not.**

### Auto-Registered Handlers
`NetworkApplicationBuilder` always registers these four — do not register them again:
- `KeyExchangeHandlers`
- `HandshakeHandlers`
- `SessionHandlers`
- `SystemControlHandlers`

### Lifecycle
- `Activate()` starts packet dispatch — does not directly manage socket open/close
- `Deactivate()` stops dispatch — listener lifecycle is separate
- Start/stop is guarded by a `SemaphoreSlim` — not reentrant; calling `Activate()` twice without `Deactivate()` will deadlock

### Resources
- `Resource.Designer.cs` is auto-generated from `Resource.resx` on every build
- **Never edit `Resource.Designer.cs` directly** — changes are silently overwritten on next build

---

## Checklists

### Bootstrap a new server
```csharp
using var host = NetworkApplication.CreateBuilder()
    .BindTcp<MyProtocol>().Bind()
    .AddHandler<MyPacketHandler>()
    .AddMiddleware<AuthMiddleware>()   // order matters
    .AddMiddleware<RateLimitMiddleware>()
    .Configure<NetworkSocketOptions>(opt => opt.Port = 8080)
    .Build();

await host.RunAsync();
```

### Add a custom protocol
1. Implement `IProtocol` — use `DefaultProtocol` as reference
2. `builder.BindTcp<MyProtocol>().Bind()` or `builder.BindUdp<MyProtocol>().Bind()`
3. Protocol receives the parsed packet before dispatch — inject pre-processing here if needed

### Add error/status strings
1. Edit `Resource.resx` — add key/value pair
2. Build — `Resource.Designer.cs` regenerates automatically
3. Access via `Resource.MyNewKey`

### Graceful shutdown
1. Call `Deactivate()` — stops new packet processing
2. Allow in-flight handler tasks to complete (await TaskManager workers if needed)
3. Close listeners
4. Call `NLogix.Host.StopAsync()` — flushes the log channel before exit

---

## Gotchas

- **Middleware registration order is execution order**: Unlike handlers (resolved by opcode), middleware runs in the exact sequence you called `AddMiddleware<>()`. If you register business logic before auth middleware, unauthenticated requests will reach business logic.

- **`Deactivate()` does not flush in-flight packets**: Stopping dispatch immediately stops processing. Packets queued in the channel but not yet handled are dropped. If graceful drain is required, wait for the dispatch channel to empty before calling `Deactivate()`.

- **`SemaphoreSlim` gate is not reentrant**: `Activate()` acquires the gate; calling it again without `Deactivate()` will block forever. This includes any code path that calls `Activate()` conditionally.

- **`Resource.Designer.cs` overwrites manual edits silently**: The `ResXFileCodeGenerator` runs on build. Any edits directly to `.Designer.cs` are lost on next `dotnet build`.

- **Handler auto-registration cannot be suppressed**: The four system handlers are always added by `NetworkApplicationBuilder`. You cannot remove or replace them via the builder API — modifying them requires forking the builder.
