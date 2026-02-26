# Nalix.Runtime API Reference

`Nalix.Runtime` is the engine that drives packet processing, handler execution, and middleware pipelines. It is the bridge between the raw network buffers and your application logic.

## Core Namespaces

### [Nalix.Runtime.Dispatching](./routing/index.md)
The dispatching layer handles the queuing and sharding of incoming packets.
- **[PacketDispatchChannel](./routing/packet-dispatch.md)**: The heart of the runtime.
- **[IPacketContext<T>](./routing/packet-context.md)**: The request-scoped context for handlers.
- **[IPacketSender<T>](./routing/packet-sender.md)**: Pooled sender for sending replies.

### [Nalix.Runtime.Middleware](./middleware/index.md)
The middleware layer allows for cross-cutting concerns like authentication and rate limiting.
- **[IPacketMiddleware<T>](./middleware/pipeline.md)**: The contract for packet-level middleware.
- **[INetworkBufferMiddleware](./middleware/network-buffer-pipeline.md)**: Raw buffer-level middleware.

### [Nalix.Runtime.Handlers](./handlers/index.md)
Classes related to handler discovery and invocation.
- **[PacketControllerAttribute](./routing/packet-attributes.md)**: Attribute to mark a class as a packet controller.
- **[PacketOpcodeAttribute](./routing/packet-attributes.md)**: Attribute to mark a method as a handler for a specific opcode.

## Design Principles
- **Zero-Allocation**: Most types in the runtime are designed to be pooled or stack-allocated.
- **Parallelism**: Shard-aware loops ensure that multicore systems are utilized effectively.
- **Extensibility**: Almost every part of the dispatch pipeline can be customized via metadata providers and custom middlewares.
