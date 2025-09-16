# Packet Dispatching System Documentation

This document describes the architecture and usage of the packet dispatching system in the `Nalix.Network.Dispatch` namespace. The system provides a high-performance, extensible, and maintainable framework for handling network packets with dependency injection, middleware, and object pooling.

---

## Overview

The packet dispatching system is designed to:

- Efficiently receive, deserialize, and route network packets to the correct handlers.
- Support both synchronous and asynchronous processing.
- Minimize memory allocations via object pooling (`PacketContext<TPacket>`) and buffer leasing.
- Provide middleware pipelines for validation, authorization, logging, and more.
- Allow easy registration of controller-based packet handlers using attributes and reflection.
- Centralize error handling and logging for robust production use.

---

## Main Components

### 1. `PacketContext<TPacket>`

- **Purpose:** Represents a reusable context for processing a network packet.
- **Features:**
  - Stores the packet, connection, metadata, and context flags.
  - Integrates with the object pool for zero-allocation reuse.
  - Provides methods to initialize, reset, and return itself to the pool.
- **SOLID/DDD:** Single-responsibility (lifecycle of a single packet). Pooling logic is infrastructure, not business logic.

### 2. `PacketDispatcherBase<TPacket>`

- **Purpose:** Abstract base class for all packet dispatchers.
- **Features:**
  - Stores configuration options, including logging and registered handlers.
  - Provides protected methods for executing handlers and resolving them by OpCode.
- **SOLID/DDD:** Open for extension (custom dispatchers), closed for modification.

### 3. `PacketDispatch` and `PacketDispatchChannel`

- **Purpose:** Implement concrete packet dispatchers.
  - `PacketDispatch`: Direct handler invocation for raw packets.
  - `PacketDispatchChannel`: Queue-based asynchronous dispatcher with background worker.
- **Features:**
  - Dependency injection for handlers and options.
  - Logging and error handling.
  - Support for high-throughput, low-latency applications.
- **SOLID/DDD:** Single-responsibility (routing and executing packets). Infrastructure layer.

### 4. `PacketDispatchOptions<TPacket>`

- **Purpose:** Stores dispatcher configuration, handler registrations, middleware, and error handlers.
- **Features:**
  - Fluent API for registering controllers and middleware.
  - Handler lookup by OpCode.
  - Custom error handling and logging hooks.

---

## Usage

### Registering Handlers

```csharp
var options = new PacketDispatchOptions<IPacket>()
    .WithLogging(myLogger)
    .WithHandler<MyPacketController>();

var dispatcher = new PacketDispatch(opts => {
    opts.WithLogging(myLogger)
        .WithHandler<MyPacketController>();
});
```

### Handling Packets

```csharp
// Synchronous dispatch
dispatcher.HandlePacket(bufferLease, connection);

// Asynchronous, queue-based dispatch (PacketDispatchChannel)
dispatcher.Activate();
dispatcher.HandlePacket(bufferLease, connection);
```

### Defining Controllers

```csharp
[PacketController]
public class MyPacketController
{
    [PacketOpcode(1)]
    public async Task HandleLogin(MyLoginPacket packet, IConnection conn)
    {
        // Your logic here
    }
}
```

---

## Example

```csharp
// Define a packet and handler
public class PingPacket : IPacket { public ushort OpCode => 100; }

[PacketController]
public class PingController
{
    [PacketOpcode(100)]
    public async Task HandlePing(PingPacket pkt, IConnection conn)
    {
        await conn.TCP.SendAsync("Pong!");
    }
}

// Register and use
var options = new PacketDispatchOptions<IPacket>()
    .WithHandler<PingController>();

var dispatcher = new PacketDispatch(opts => opts.WithHandler<PingController>());

// On network event:
dispatcher.HandlePacket(bufferLease, connection);
```

---

## Notes & Security

- **Object Pooling:** Always return contexts to the pool to avoid memory leaks.
- **Thread Safety:** All components are thread-safe and ready for concurrent usage.
- **Error Handling:** Handlers should not throw unhandled exceptions; use custom error handlers for production.
- **Extensibility:** Add middleware for cross-cutting concerns (auth, validation, logging).
- **Performance:** Uses aggressive inlining, pooling, and background workers for maximum throughput.
- **Domain Separation:** Handler/controller logic should remain domain-focused; dispatchers handle infrastructure concerns.

---

## SOLID & DDD Principles

- **Single Responsibility:** Each class has a focused purpose (e.g., context, dispatching, options).
- **Open/Closed:** New packet types or handlers can be added without modifying core logic.
- **Liskov Substitution:** All dispatchers implement a common interface and can be swapped.
- **Interface Segregation:** Only necessary methods are exposed for each component.
- **Dependency Inversion:** Uses interfaces (`ILogger`, `IConnection`, etc.) and DI for flexibility.

---

## Troubleshooting

- Ensure all controllers have the `[PacketController]` attribute and handler methods have `[PacketOpcode]`.
- Register all required dependencies (e.g., `IPacketCatalog`, `ILogger`) via your DI container.
- For high-throughput workloads, use `PacketDispatchChannel` and tune pool sizes and queue depths.

---

## Additional Remarks

- Fully compatible with Visual Studio and VS Code.
- Designed for scalability, maintainability, and clean code practices.
- Automatically integrates with modern .NET dependency injection and logging.

---
