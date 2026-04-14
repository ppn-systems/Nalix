# Nalix.Runtime API Reference

`Nalix.Runtime` is the server-side execution layer that converts incoming packets into handler invocations through dispatch channels, packet contexts, metadata resolution, and middleware pipelines.

## Source Mapping

- `src/Nalix.Runtime/Dispatching`
- `src/Nalix.Runtime/Middleware`
- `src/Nalix.Runtime/Handlers`
- `src/Nalix.Runtime/Options`

## Why This Package Exists

`Nalix.Network` accepts traffic and manages connections, but packet execution policy lives in `Nalix.Runtime`. This split allows transport and handler execution to evolve independently.

## Mental Model

1. A packet arrives (raw buffer or already deserialized packet).
2. `IPacketDispatch` handles it via `PacketDispatchChannel`.
3. Packet metadata is resolved for handler execution.
4. A pooled `PacketContext<TPacket>` is created.
5. Handler pipeline executes with middleware and optional outbound transforms.

## Core Public Types

### Dispatching

- [IPacketDispatch](./routing/dispatch-contracts.md): entry point for handling incoming packets.
- [PacketDispatchChannel](./routing/packet-dispatch.md): high-throughput dispatcher with worker loops and wake signaling.
- [PacketDispatcherBase<TPacket>](./routing/dispatch-channel-and-router.md): base type for dispatch execution and handler invocation.
- [PacketContext<TPacket>](./routing/packet-context.md): pooled per-dispatch context implementing `IPacketContext<TPacket>`.
- [PacketSender<TPacket>](./routing/packet-sender.md): metadata-aware sender used by packet contexts.
- [IPacketMetadataProvider](./routing/packet-metadata.md), [PacketMetadataBuilder](./routing/packet-metadata.md), [PacketMetadataProviders](./routing/packet-metadata.md): metadata resolution surface.

### Middleware

- [NetworkBufferMiddlewarePipeline](./middleware/network-buffer-pipeline.md): inbound raw-buffer middleware pipeline.
- Middleware contracts are shared in `Nalix.Common` (`IPacketMiddleware<TPacket>`, `INetworkBufferMiddleware`).

### Built-in Handlers

- [HandshakeHandlers](./handlers/index.md)
- [SessionHandlers](./handlers/index.md)
- [SystemControlHandlers](./handlers/index.md)

### Runtime Options

- [DispatchOptions](./options/dispatch-options.md)
- Runtime pooling options are exposed by `Nalix.Runtime.Options.PoolingOptions`.

## Architecture Notes

- `PacketContext<TPacket>` and `PacketSender<TPacket>` are pool-oriented types.
- `PacketDispatchChannel` supports both raw buffer (`IBufferLease`) and typed packet dispatch paths.
- Middleware is split by stage (inbound/outbound) and can be configured to continue or stop on errors.

## Related APIs

- [Runtime Routing Overview](./routing/index.md)
- [Runtime Middleware Overview](./middleware/index.md)
- [Network Protocol](../network/protocol.md)
- [Packet Contracts](../common/packet-contracts.md)
