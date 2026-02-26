# Packet Attributes

This library provides annotation attributes for packet handler/controller methods in .NET server backends.
You use these attributes to declaratively control **dispatch, security, rate limiting, concurrency, timeout, and encryption** for packet handlers.

---

## Source mapping

- `src/Nalix.Common/Networking/Packets/PacketControllerAttribute.cs`
- `src/Nalix.Common/Networking/Packets/PacketOpcodeAttribute.cs`
- `src/Nalix.Common/Networking/Packets/PacketPermissionAttribute.cs`
- `src/Nalix.Common/Networking/Packets/PacketConcurrencyLimitAttribute.cs`
- `src/Nalix.Common/Networking/Packets/PacketRateLimitAttribute.cs`
- `src/Nalix.Common/Networking/Packets/PacketEncryptionAttribute.cs`
- `src/Nalix.Common/Networking/Packets/PacketTimeoutAttribute.cs`

## Overview Table

| Attribute                         |  Used On  | Purpose / Controls                                         |
|-----------------------------------|-----------|------------------------------------------------------------|
| `PacketControllerAttribute`       | class     | Declares a controller (group of handlers) and its metadata |
| `PacketOpcodeAttribute`           | method    | Sets command/packet OpCode                                 |
| `PacketPermissionAttribute`       | method    | Min permission required to execute                         |
| `PacketConcurrencyLimitAttribute` | method    | Limits concurrent executions, with queue option            |
| `PacketRateLimitAttribute`        | method    | Limits request rate, supports burst window                 |
| `PacketEncryptionAttribute`       | method    | Requires encryption, algorithm selection                   |
| `PacketTimeoutAttribute`          | method    | Sets max processing time (ms); triggers fail timeout       |

---

## Basic usage

```csharp
[PacketOpcode(0x1802)]
[PacketPermission(PermissionLevel.USER)]
[PacketRateLimit(8, burst: 1.5)]
public static Task HandleAsync(IPacketContext<IPacket> request)
{
    return Task.CompletedTask;
}
```

## Supported Handler Return Types

Each packet handler method can use the following return types.  
The framework will automatically select the correct result handler based on the return type:

| Return Type                                      | Description                                                  |
|--------------------------------------------------|--------------------------------------------------------------|
| `void`                                           | Synchronous, no return value (fire-and-forget)               |
| `Task`                                           | Asynchronous, no return value                                |
| `ValueTask`                                      | Lightweight asynchronous, no return value                    |
| `T` (e.g. your Packet class)                     | Returns a response packet directly                           |
| `Task<T>`                                        | Asynchronous with response packet/data                       |
| `ValueTask<T>`                                   | Lightweight async with response packet/data                  |
| `byte[]`, `Memory<byte>`                   | Simple buffers—sent as-is                                    |
| `ReadOnlyMemory<byte>`                           | For high-performance buffer return                           |

- `T` is typically your packet or result type that implements `IPacket`.
- If an unsupported return type is used, the system will throw or reject the method signature at runtime or registration.

---

## 1. `PacketControllerAttribute`

Marks a class as a *packet controller* and defines its metadata.

```csharp
[PacketController(name: "GameControl", isActive: true, version: "2.0")]
public class GameProtocolController { ... }
```

- **name:** Friendly name for debug/logging
- **isActive:** If false, handler is ignored
- **version:** Version for migration/support

**Supported handler shapes:**

The compiler currently accepts these method signatures:

| Style | Signature | Notes |
|---|---|---|
| Context | `IPacketContext<TPacket>` | Modern context-aware handler. `context.Packet`, `context.Connection`, `context.Attributes`, and `context.Sender` are available from the single parameter. |
| Context | `IPacketContext<TPacket>, CancellationToken` | Same as above, with an explicit cancellation token parameter. |
| Legacy | `TPacket, IConnection` | Older packet+connection signature that remains supported for compatibility. |
| Legacy | `TPacket, IConnection, CancellationToken` | Legacy signature with explicit cancellation token support. |

Return types can be:

| Return type | Behavior |
|---|---|
| `void` | No return payload. |
| `Task` | Async handler with no return payload. |
| `ValueTask` | Lightweight async handler with no return payload. |
| `TPacket` | Returns a packet directly. |
| `byte[]` | Sends the byte array payload as raw bytes. |
| `Memory<byte>` | Sends the memory payload as raw bytes. |
| `ReadOnlyMemory<byte>` | Sends the read-only memory payload as raw bytes. |
| `Task<T>` | Async wrapper around any supported non-task result type. |
| `ValueTask<T>` | Lightweight async wrapper around any supported non-task result type. |

Invalid signatures, duplicate opcodes, or mismatched context packet types are rejected during scanning/compilation.

---

## 2. `PacketOpcodeAttribute`

Assigns a unique OpCode to a handler method.

```csharp
[PacketOpcode(0x1802)]
public Task HandleLogin(PacketContext<IPacket> request) { ... }
```

- **OpCode:** `ushort` value (matches wire/protocol commands)

---

## 3. `PacketPermissionAttribute`

Sets minimum permission required for handler.

```csharp
[PacketPermission(PermissionLevel.ADMIN)]
public Task HandleDelete(PacketContext<IPacket> request) { ... }
```

- Enforces security policy: Only clients with level ≥ specified can invoke.

---

## 4. `PacketConcurrencyLimitAttribute`

Limits concurrent execution per handler; supports queue if overflow.

```csharp
[PacketConcurrencyLimit(4, queue: true, queueMax: 32)]
public Task HandleExpensiveTask(PacketContext<IPacket> request) { ... }
```

- **Max:** maximum concurrent executions allowed
- **Queue:** true enables waiting in a queue (instead of hard-reject)
- **QueueMax:** cap queue length (if 0, reject when full)

---

## 5. `PacketRateLimitAttribute`

Limits how many requests per second can be processed (with burst support).

```csharp
[PacketRateLimit(8, burst: 1.5)]
public Task HandlePing(PacketContext<IPacket> request) { ... }
```

- **RequestsPerSecond:** max rate allowed
- **Burst:** burst multiplier (defaults to 1)

---

## 6. `PacketEncryptionAttribute`

Requires packets to be encrypted when sent/received; allows algorithm selection.

```csharp
[PacketEncryption(isEncrypted: true, algorithmType: CipherSuiteType.Salsa20Poly1305)]
public Task HandleSecret(PacketContext<IPacket> request) { ... }
```

- **IsEncrypted:** if true, encryption must be applied
- **AlgorithmType:** select cipher suite (ChaCha, Salsa, ...)

---

## 7. `PacketTimeoutAttribute`

Sets per-handler max processing time (in milliseconds).

```csharp
[PacketTimeout(2000)] // 2 seconds
public Task HandleLongTask(PacketContext<IPacket> request) { ... }
```

- Throws timeout/fail response if exceeded

---

## Full Example Controller

```csharp
[PacketController("ExampleCtrl")]
public class ExampleCtrl
{
    [PacketOpcode(0x1001)]
    [PacketPermission(PermissionLevel.USER)]
    [PacketConcurrencyLimit(2, queue: true, queueMax: 8)]
    [PacketRateLimit(5, burst: 2)]
    [PacketEncryption(isEncrypted: true)]
    [PacketTimeout(8000)]
    public async Task HandleChat(PacketContext<IPacket> request, CancellationToken ct)
    {
        // Handler implementation...
    }
}
```

---

## Best Practices

!!! tip "Quick apply"
    - Always set `PacketOpcode` and `PacketPermission`.  
    - Add `PacketTimeout` for long-running handlers.  
    - Use `PacketEncryption` for PII/secret flows.  
    - Cap concurrency and rate on public endpoints.

- Always annotate with `PacketOpcode` for dispatcher to work
- Use permission/concurrency/rate/timeout for robust production command protection
- Encryption required for business-critical/PII
- Timeout required for potentially long-running tasks (defensive fail)

## Related APIs

- [Packet Metadata](./packet-metadata.md)
- [Packet Context](./packet-context.md)
- [Handler Results](./handler-results.md)
- [Middleware Pipeline](../middleware/pipeline.md)
