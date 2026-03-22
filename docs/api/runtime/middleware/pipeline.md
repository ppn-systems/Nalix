# Middleware Pipeline

Nalix.Network has two middleware layers:

- buffer middleware before deserialization
- packet middleware around handler execution

## Source mapping

- `src/Nalix.Runtime/Middleware/NetworkBufferMiddlewarePipeline.cs`
- `src/Nalix.Common/Middleware/INetworkBufferMiddleware.cs`
- `src/Nalix.Runtime/Middleware/MiddlewarePipeline.cs`
- `src/Nalix.Common/Middleware/IPacketMiddleware.cs`

## Buffer middleware

Buffer middleware works on raw `IBufferLease` data before a packet exists.

Use it for:

- decryption
- decompression
- low-level validation
- early frame rejection

Contract:

```csharp
ValueTask<IBufferLease?> InvokeAsync(
    IBufferLease buffer,
    IConnection connection,
    CancellationToken ct)
```

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
| `SequenceId`| `uint` | Used for request/reply correlation. |
| `Flags` | `PacketFlags` | Metadata like `IsResponse`, `IsEncrypted`, or `IsCompressed`. |
| `Priority` | `PacketPriority` | Dispatcher hint (Base, Priority, Urgent). |
| `Protocol` | `ProtocolType` | Transport layer (TCP/UDP/None). |
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

public sealed class AuditMiddleware : IPacketMiddleware
{
    private readonly ILogger _logger;

    public AuditMiddleware(ILogger logger) => _logger = logger;

    public async ValueTask InvokeAsync<TPacket>(
        IPacketContext<TPacket> context, 
        Func<CancellationToken, ValueTask> next) 
        where TPacket : IPacket
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
  -> buffer middleware
  -> deserialize packet
  -> packet middleware
  -> handler
  -> response path
```

## Basic usage

```csharp
using Nalix.Runtime.Dispatching;
using Nalix.Network.Hosting;

options.NetworkPipeline.Use(new SampleAuditBufferMiddleware());
options.PacketPipeline.Use(new SampleAuditMiddleware());
```

## Related APIs

- [Network Buffer Pipeline](./network-buffer-pipeline.md)
- [Connection Limiter](../../network/connection/connection-limiter.md)
- [Concurrency Gate](./concurrency-gate.md)
- [Policy Rate Limiter](./policy-rate-limiter.md)
- [Token Bucket Limiter](./token-bucket-limiter.md)
- [Packet Dispatch](../routing/packet-dispatch.md)
- [Packet Metadata](../routing/packet-metadata.md)
