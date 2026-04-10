# Nalix.Runtime.Dispatching

The dispatching layer is responsible for the parallel processing of network packets. It handles sharding, queuing, and the transition from raw buffers to typed packet objects.

## Core Components

### [PacketDispatchChannel](./packet-dispatch.md)
The central engine that manages worker loops and packet sharding.

### [IPacketContext<T>](./packet-context.md)
The request-scoped context carrying the packet, connection, and metadata.

### [IPacketSender<T>](./packet-sender.md)
The interface for sending replies back through the connection with automatic metadata application.

## Other Resources

- [Dispatch Contracts](./dispatch-contracts.md)
- [Handler Result Types](./handler-results.md)
- [Packet Metadata](./packet-metadata.md)
- [Packet Attributes](./packet-attributes.md)
- [Dispatch Channel and Router Internal Details](./dispatch-channel-and-router.md)
