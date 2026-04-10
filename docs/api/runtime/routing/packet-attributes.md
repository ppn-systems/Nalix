# Packet Attributes

Nalix uses a declarative, attribute-based model for securing and configuring packet handlers. By annotating your controllers and methods, you can enforce high-performance security, rate limiting, and concurrency policies without polluting your business logic.

## Overview Table

| Attribute | Scope | Primary Purpose |
|---|---|---|
| `[PacketController]` | Class | Declares a logical group of handlers with shared metadata. |
| `[PacketOpcode]` | Method| Assigns the 16-bit Operation Code (OpCode) for dispatch. |
| `[PacketPermission]`| Method| Enforces minimum `PermissionLevel` required for execution. |
| `[PacketRateLimit]` | Method| Protects against spam via token-bucket rate limiting. |
| `[PacketConcurrency]`| Method| Limits simultaneous execution to prevent resource exhaustion. |
| `[PacketEncryption]`| Method| Forces AEAD encryption on both request and response. |
| `[PacketTimeout]` | Method| Enforces processed-time limits and triggers client timeouts. |

## 1. Dispatch Attributes

### `[PacketController]`
Groups related handlers and provides identification for the dispatcher.

- **Name**: Friendly identifier for logs and telemetry.
- **IsActive**: Allows for soft-disabling an entire module at runtime.
- **Version**: Versioning hint for handler discovery.

### `[PacketOpcode]`
The fundamental routing attribute. This constant maps exactly to the OpCode header in the wire protocol.

```csharp
[PacketOpcode(0x1001)]
public ValueTask Handle(PacketContext<LoginPacket> context) { ... }
```

## 2. Security & Guard Attributes

### `[PacketPermission]`
Requires the current `IConnection.Level` to be greater than or equal to the specified value.

- **Level**: One of `PermissionLevel` constants (e.g., `USER`, `SYSTEM_ADMINISTRATOR`).

```csharp
[PacketPermission(PermissionLevel.SYSTEM_ADMINISTRATOR)]
public void ResetServer(PacketContext<Control> context) { ... }
```

### `[PacketEncryption]`
Ensures that the packet is only processed if it arrives encrypted, and forces encryption on the response.

- **IsEncrypted**: Boolean flag to enable/disable.
- **AlgorithmType**: Optional hint for the preferred `CipherSuiteType`.

## 3. Reliability & QoS Attributes

### `[PacketRateLimit]`
Implements a token-bucket limiter for the handler.

- **RequestsPerSecond**: The sustained rate.
- **Burst**: A multiplier for handling temporary spikes (default 1.0).

### `[PacketConcurrencyLimit]`
Limits the number of threads currently executing this specific handler logic.

- **Max**: The hard concurrency cap.
- **Queue**: If true, overflowing requests wait in a FIFO queue instead of being dropped.
- **QueueMax**: The maximum size of the waiting queue.

### `[PacketTimeout]`
The server-side equivalent of a request timeout.

- **TimeoutMilliseconds**: If the handler doesn't return within this window, the pipeline cancels the `CancellationToken` and sends a `TIMEOUT` directive.

## Implementation Example

```csharp
[PacketController("SecureChat", version: "1.2")]
public class SecureChatController
{
    [PacketOpcode(0x3001)]
    [PacketPermission(PermissionLevel.USER)]
    [PacketRateLimit(10, burst: 2.0)]
    [PacketConcurrencyLimit(100, queue: true)]
    [PacketEncryption(true)]
    [PacketTimeout(5000)]
    public async ValueTask Handle(IPacketContext<ChatPacket> context, CancellationToken ct)
    {
        // Protected, rate-limited, and encrypted handler logic
    }
}
```

## Best Practices

- **Security First**: Every public-facing handler should have a `PacketPermission` and ideally a `PacketRateLimit`.
- **Defensive Timeouts**: Wrap potentially heavy compute or I/O tasks in a `PacketTimeout` to prevent thread-pool starvation.
- **Low-Latency Concurrency**: For high-volume handlers, set `PacketConcurrencyLimit` to prevent a single expensive request type from overwhelming the system.
- **OpCode Management**: Maintain a central registry of OpCodes to prevent collisions during development.

## Related APIs

- [Packet Context](./packet-context.md)
- [Packet Metadata](./packet-metadata.md)
- [Permission Levels](../../security/permission-level.md)
- [Handler Results](./handler-results.md)
- [Middleware Pipeline](../middleware/pipeline.md)
