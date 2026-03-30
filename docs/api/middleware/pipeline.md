# Middleware Pipeline

Nalix.Network has two middleware layers:

- buffer middleware before deserialization
- packet middleware around handler execution

## Source mapping

- `src/Nalix.Network/Middleware/NetworkBufferMiddlewarePipeline.cs`
- `src/Nalix.Network/Middleware/INetworkBufferMiddleware.cs`
- `src/Nalix.Network/Middleware/MiddlewarePipeline.cs`
- `src/Nalix.Network/Middleware/IPacketMiddleware.cs`

## Buffer middleware

Buffer middleware works on raw `IBufferLease` data before a packet exists.

Use it for:

- decryption
- decompression
- low-level validation
- early frame rejection

Contract:

```csharp
Task<IBufferLease> InvokeAsync(
    IBufferLease buffer,
    IConnection connection,
    Func<IBufferLease, CancellationToken, Task<IBufferLease>> next,
    CancellationToken ct)
```

## Packet middleware

Packet middleware works on `PacketContext<TPacket>` after deserialization.

Use it for:

- permissions
- timeouts
- rate limiting
- concurrency limits
- auditing

Contract:

```csharp
Task InvokeAsync(
    PacketContext<TPacket> context,
    Func<CancellationToken, Task> next)
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
options.NetworkPipeline.Use(new SampleAuditBufferMiddleware());
options.PacketPipeline.Use(new SampleAuditMiddleware());
```

## Related APIs

- [Network Buffer Pipeline](./network-buffer-pipeline.md)
- [Connection Limiter](./connection-limiter.md)
- [Concurrency Gate](./concurrency-gate.md)
- [Policy Rate Limiter](./policy-rate-limiter.md)
- [Token Bucket Limiter](./token-bucket-limiter.md)
- [Packet Dispatch](../routing/packet-dispatch.md)
- [Packet Metadata](../routing/packet-metadata.md)
