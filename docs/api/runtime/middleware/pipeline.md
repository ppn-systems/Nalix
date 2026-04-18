# Middleware Pipeline

The Nalix runtime supports high-performance packet middleware that executes around handler execution, allowing for cross-cutting concerns like security, throttling, and observability.

## Source mapping

- `src/Nalix.Runtime/Middleware/MiddlewarePipeline.cs`
- `src/Nalix.Common/Middleware/IPacketMiddleware.cs`

## Packet middleware

Packet middleware works on `IPacketContext<TPacket>` after deserialization.
The same middleware model applies to built-in packets and custom packet types.

Use it for:

- permissions
- timeouts
- rate limiting
- concurrency limits
- auditing

Contract:

```csharp
ValueTask InvokeAsync(
    IPacketContext<TPacket> context,
    Func<CancellationToken, ValueTask> next)
```

## Ordering

Ordering is driven by:

- `[MiddlewareOrder]`
- `[MiddlewareStage]`

The packet pipeline supports:

- `Inbound`
- `Outbound`
- `Both`

## Built-in middleware

Common built-in packet middleware:

- `PermissionMiddleware`
- `RateLimitMiddleware`
- `ConcurrencyMiddleware`
- `TimeoutMiddleware`

`RateLimitMiddleware` uses handler-specific metadata when a packet has `[PacketRateLimit(...)]` and otherwise falls back to the global per-endpoint token bucket. That means reserved/control packets without an explicit attribute still go through ingress throttling.

---

## Diving into Packet Headers

Middlewares often need to inspect packet headers for auditing, routing, or security. Since the `IPacketContext<TPacket>` exposes the `Packet` (which implements `IPacket`), you have zero-allocation access to the following fields:

| Field | Type | Purpose |
| :--- | :--- | :--- |
| `OpCode` | `ushort` | Identifies the handler/message type. |
| `SequenceId`| `ushort` | Used for request/reply correlation. |
| `Flags` | `PacketFlags` | Metadata like `RELIABLE`, `SYSTEM`, or `COMPRESSED`. |
| `Priority` | `PacketPriority` | Dispatcher hint (`NONE`, `LOW`, `MEDIUM`, `HIGH`, `URGENT`). |
| `IsReliable` | `bool` | Indicates if the transport provides reliability (TCP). |
| `Length` | `int` | Total wire size of the packet. |
| `MagicNumber`| `uint` | Unique type identity hash. |

### Example: Comprehensive Audit Middleware

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nalix.Common.Middleware;
using Nalix.Common.Networking.Packets;

public sealed class AuditMiddleware : IPacketMiddleware<IPacket>
{
    private readonly ILogger _logger;

    public AuditMiddleware(ILogger logger) => _logger = logger;

    public async ValueTask InvokeAsync(
        IPacketContext<IPacket> context,
        Func<CancellationToken, ValueTask> next)
    {
        var packet = context.Packet;
        
        // Log headers before execution
        _logger.Info($@"[INBOUND] 
            OpCode: 0x{packet.OpCode:X4} 
            SequenceId: {packet.SequenceId} 
            Priority: {packet.Priority} 
            Size: {packet.Length} bytes");

        // Continue to the next middleware or handler
        await next(context.CancellationToken);
        
        // Post-execution logging (optional)
        _logger.Debug($"Successfully processed 0x{packet.OpCode:X4}");
    }
}
```

---

## Mental model

```text
socket buffer
  -> translate IBufferLease
  -> deserialize packet
  -> packet middleware
  -> handler
  -> response path
```

## Basic usage

```csharp
using Nalix.Runtime.Dispatching;
using Nalix.Network.Hosting;

options.WithMiddleware(new SampleAuditMiddleware());
```

## Related APIs

- [Connection Limiter](../../network/connection/connection-limiter.md)
- [Concurrency Gate](./concurrency-gate.md)
- [Policy Rate Limiter](./policy-rate-limiter.md)
- [Token Bucket Limiter](./token-bucket-limiter.md)
- [Packet Dispatch](../routing/packet-dispatch.md)
- [Packet Metadata](../routing/packet-metadata.md)
