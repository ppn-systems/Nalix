# Nalix.Runtime.Dispatching

`Nalix.Runtime.Dispatching` contains the execution APIs that map incoming packets to handlers with metadata-aware context and pooled senders.

## Source Mapping

- `src/Nalix.Runtime/Dispatching`
- `src/Nalix.Runtime/Internal/Routing`
- `src/Nalix.Runtime/Internal/Compilation`

## Why This Layer Exists

The dispatch layer isolates handler execution policy from socket/listener concerns. `Nalix.Network` can focus on transport while dispatching focuses on packet-to-handler routing and middleware orchestration.

## Public Surface

### Contracts

- [IDispatchChannel<TPacket>](./dispatch-channel-and-router.md): queue contract for connection-aware packet routing.
- [IPacketDispatch](./dispatch-contracts.md): high-level dispatch entry points (`IBufferLease` and `IPacket` overloads).
- [IPacketMetadataProvider](./packet-metadata.md): metadata provider abstraction.

### Core Implementations

- [PacketDispatchChannel](./packet-dispatch.md): runtime dispatcher used in production packet processing.
- [PacketContext<TPacket>](./packet-context.md): pooled handler context.
- [PacketSender<TPacket>](./packet-sender.md): send API honoring context metadata.
- [PacketMetadataBuilder](./packet-metadata.md): builds packet metadata.
- [PacketMetadataProviders](./packet-metadata.md): predefined provider helpers.

### Base/Advanced

- [PacketDispatcherBase<TPacket>](./dispatch-channel-and-router.md): base dispatch implementation.
- `Nalix.Runtime.Internal.Routing.DispatchChannel<TPacket>` is public but located in an internal namespace; treat as advanced infrastructure API.

## Mental Model

```mermaid
flowchart LR
    A["IBufferLease or IPacket"] --> B["IPacketDispatch"]
    B --> C["PacketDispatchChannel"]
    C --> D["IPacketRegistry"]
    D --> E["PacketContext<TPacket>"]
    E --> F["Middleware + Handler"]
    F --> G["PacketSender<TPacket>"]
```

## Best Practices

- Register packet metadata providers before dispatch activation.
- Use `IPacketContext<TPacket>.Sender` inside handlers instead of bypassing context.
- Prefer `HandlePacket(IBufferLease, IConnection)` for normal inbound transport flow.

## Related APIs

- [Packet Dispatch](./packet-dispatch.md)
- [Packet Context](./packet-context.md)
- [Packet Metadata](./packet-metadata.md)
- [Runtime Overview](../index.md)
