# Network Buffer Pipeline

This page covers the raw-byte middleware surface that runs before packet deserialization.

## Source mapping

- `src/Nalix.Network/Middleware/NetworkBufferMiddlewarePipeline.cs`
- `src/Nalix.Network/Middleware/INetworkBufferMiddleware.cs`

## Main types

- `INetworkBufferMiddleware`
- `NetworkBufferMiddlewarePipeline`

## INetworkBufferMiddleware

`INetworkBufferMiddleware` is the contract for middleware that works directly on `IBufferLease` and `IConnection`.

It can:

- inspect incoming frame bytes
- mutate or replace the buffer
- stop the pipeline early
- reject malformed or unwanted traffic before deserialization

### Contract

```csharp
Task<IBufferLease?> InvokeAsync(
    IBufferLease buffer,
    IConnection connection,
    Func<IBufferLease, CancellationToken, Task<IBufferLease?>> nextHandler,
    CancellationToken ct);
```

### Ownership rule

If middleware replaces the incoming lease with a new one, it should also define what happens to the original lease, usually by disposing it after the replacement is safe.

Returning `null` means the frame was fully handled, dropped, or intentionally short-circuited.

## NetworkBufferMiddlewarePipeline

`NetworkBufferMiddlewarePipeline` is the concrete ordered chain that executes registered raw-buffer middleware.

It provides:

- `Use(...)`
- `Clear()`
- `ExecuteAsync(...)`

### Source behavior

- duplicate middleware instances are rejected
- execution order comes from `[MiddlewareOrder]`
- the pipeline takes a snapshot before execution instead of holding locks during invocation
- middleware compose into a delegate chain similar to ASP.NET Core

## Basic usage

```csharp
NetworkBufferMiddlewarePipeline pipeline = new();
pipeline.Use(new FrameGuard());
pipeline.Use(new MyDecryptMiddleware());

IBufferLease? next = await pipeline.ExecuteAsync(buffer, connection, ct);
```

## When to use this layer

Use raw-buffer middleware when the packet does not exist yet and your logic needs to act on bytes.

Typical use cases:

- decryption
- decompression
- frame signature checks
- protocol-level rejection before deserialization

If you already need `PacketContext<TPacket>` and handler metadata, use packet middleware instead.
That same rule applies when the packet type is custom, not just when it is one of the built-in packets.

## Relationship to packet middleware

```text
socket frame
  -> network buffer middleware
  -> deserialize packet
  -> packet middleware
  -> handler
```

## Related APIs

- [Middleware Pipeline](./pipeline.md)
- [Packet Dispatch](../routing/packet-dispatch.md)
- [Socket Connection](../../network/socket-connection.md)
- [Buffer and Pooling](../../framework/memory/buffer-and-pooling.md)
