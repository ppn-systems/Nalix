# Nalix.Runtime

`Nalix.Runtime` is the high-level orchestration layer for the Nalix framework. It provides the request/response pipeline, packet dispatching, middleware execution, and handler compilation.

## Key Features

- **Asynchronous Dispatcher:** High-performance priority queue for processing network packets.
- **Middleware Pipeline:** Extensible pipeline for both raw buffers and typed packets.
- **Handler Compilation:** Uses Expressions/IL to compile handler methods into high-speed delegates, avoiding reflection during the hot path.
- **Metadata Management:** Bridges declarative attributes to runtime behavior.
- **Session Resume:** Restores authenticated connection state through the dedicated `SessionResume` packet flow.

## Core Components

### [Packet Dispatch](../api/runtime/routing/packet-dispatch.md)

The `PacketDispatchChannel` manages worker loops that pull packets from the queue, run them through middleware, and execute the correct handler.
It supports the same generic handler model used by built-in packets and custom packet types.

### [Middleware](../api/runtime/middleware/pipeline.md)

Supports both `NetworkBufferMiddleware` (for raw bytes) and `PacketMiddleware` (for deserialized objects), allowing for features like compression, encryption, and logging.

### [Routing](../api/runtime/routing/packet-attributes.md)

Attribute-based routing allows you to define packet handlers simply by annotating methods with `[PacketOpcode]`.

### [Session Resume](../api/security/session-resume.md)

The built-in resume flow is handled by `SessionHandlers` and backed by `ISessionManager`.

## Usage

`Nalix.Runtime` is typically consumed via `Nalix.Network.Hosting`, which wires up the dispatcher and middleware automatically.

```csharp
// Normally used inside NetworkApplication
var dispatcher = new PacketDispatchChannel(options => {
    options.WithLogging(logger);
    options.WithHandler<MyHandler>();
});

dispatcher.Activate(ct);
```

## Related Packages

- [Nalix.Network](./nalix-network.md): Low-level networking primitives.
- [Nalix.Framework](./nalix-framework.md): Core utilities and data frames.
- [Nalix.Network.Hosting](./nalix-network-hosting.md): Application bootstrap.
