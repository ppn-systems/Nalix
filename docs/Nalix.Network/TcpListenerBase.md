# TcpListenerBase Documentation

## Overview

The `TcpListenerBase` class (namespace: `Nalix.Network.Listeners.Tcp`) is an abstract base class for building scalable, high-performance TCP network listeners in .NET. It manages the entire connection lifecycle: accepting sockets, initializing network connections, integrating with protocols, handling concurrency, and providing robust error handling and reporting. The design follows SOLID and DDD principles, ensuring extensibility and maintainability for production-grade server applications.

---

## Functional Summary

- **Connection Acceptance:** Handles both sync and async acceptance of incoming TCP connections.
- **Resource Pooling:** Uses object pools for accept contexts and socket event arguments for minimal allocations and high throughput.
- **Socket Configuration:** Configures sockets for optimal performance (buffer sizes, timeouts, keep-alive, etc.).
- **Protocol Integration:** Associates each connection with a protocol handler for message processing.
- **Event Management:** Subscribes/unsubscribes to connection events to prevent memory leaks.
- **Concurrency:** Manages multiple parallel accept loops, leveraging the .NET thread pool.
- **Graceful Shutdown:** Handles cancellation, error backoff, and cleanup for robust operation.
- **Diagnostics:** Provides detailed runtime and configuration reports for monitoring.
- **Time Synchronization:** Supports optional time sync for distributed environments.
- **Extensibility:** Designed for easy subclassing and protocol customization.

---

## Detailed Structure and Explanation

### Fields and Properties

- `Config`: Static, loaded from `NetworkSocketOptions`, holds runtime socket settings.
- `_port`, `_protocol`: Assigned at construction; define listener endpoint and protocol logic.
- `_listener`: The underlying `Socket` object for accepting new connections.
- `_lock`, `_cts`, `_cancellationToken`: Used for thread safety and cooperative cancellation.
- `_acceptWorkerIds`: Tracks IDs of acceptor worker threads for management.
- `State`: Enum property indicating the listener state (Stopped, Starting, Running, Stopping).
- `IsTimeSyncEnabled`: Property to enable/disable time synchronization.

### Initialization

- **Constructor:**  
  Initializes fields, configures pooling for accept contexts and socket args, and wires up time sync events.

- **Initialize():**  
  Sets up the underlying socket for IPv4 or IPv6, configures OS-specific options, and prepares for listening.

### Public API

- **Activate(CancellationToken):**  
  Starts the listener, launches parallel accept workers, and activates protocol/time sync as needed.

- **Deactivate(CancellationToken):**  
  Stops the listener, cancels workers, releases all resources, and closes active connections.

- **Dispose():**  
  Ensures all resources are released and the listener is properly cleaned up.

- **GenerateReport():**  
  Returns a string report of the current state, configuration, and metrics.

### Internal Connection Flow

- **AcceptConnectionsAsync():**  
  Main loop for accepting new sockets, creating connections, and dispatching workers for processing.

- **CreateConnectionAsync():**  
  Waits for a new connection, validates it (including IP rate limiting), and initializes the connection object.

- **InitializeConnection(Socket, PooledAcceptContext):**  
  Configures the socket, wires up protocol/events, and returns the connection. Always returns the context to the pool.

- **ProcessConnection(IConnection):**  
  Passes the connection to the protocol handler and logs errors.

- **HandleConnectionClose():**  
  Unsubscribes all events and disposes the connection to prevent memory/resource leaks.

- **Socket Configuration:**  
  Applies OS-specific performance settings (NoDelay, buffers, keep-alive, etc.) for optimal operation.

---

## Usage

```csharp
// Subclass TcpListenerBase to implement your own protocol.
public class MyTcpListener : TcpListenerBase
{
    public MyTcpListener(IProtocol protocol) : base(protocol) { }

    public override void SynchronizeTime(long milliseconds)
    {
        // Custom time sync logic if needed
    }
}

// Start listening
var listener = new MyTcpListener(new MyProtocolHandler());
listener.Activate();

// ... Later, when shutting down
listener.Deactivate();
listener.Dispose();
```

---

## Example

```csharp
// Custom protocol implementation
public class MyProtocol : IProtocol
{
    public void OnAccept(IConnection conn, CancellationToken token)
    {
        // Handle new connection logic
    }
    // ... more protocol methods
}

// Use in your application
var protocol = new MyProtocol();
var tcpListener = new MyTcpListener(protocol);
tcpListener.Activate();

// To stop:
tcpListener.Deactivate();
tcpListener.Dispose();
```

---

## Notes & Security

- **Resource Management:** Always call `Deactivate()` and `Dispose()` to avoid resource leaks.
- **Thread Safety:** All operations are thread-safe and designed for concurrent environments.
- **Pooling:** Utilizes pooling for minimum GC pressure and fastest object reuse.
- **Error Handling:** All errors are logged. Only non-fatal errors are retried; fatal errors lead to shutdown.
- **Configuration:** Socket and concurrency settings are read from `NetworkSocketOptions` and can be tuned for your workload.
- **Extensibility:** Subclass and override methods as needed, especially for protocol integration.
- **Security:** Connection limiting is enforced via `ConnectionLimiter` to prevent DoS attacks.

---

## SOLID & DDD Principles

- **Single Responsibility:** Each method and class is focused on a single concern (accepting, processing, configuring, etc.).
- **Open/Closed:** Easily extended for new protocols or connection types.
- **Liskov Substitution:** Can be replaced by subclasses without altering client code.
- **Interface Segregation:** Implements only necessary interfaces and separates concerns.
- **Dependency Inversion:** Depends on abstractions (`IProtocol`, `ILogger`) for flexibility and testability.

**Domain-Driven Design:**  
Infrastructure logic (socket, pooling, threading) is separated from domain/protocol logic, keeping your business code clean and maintainable.

---

## Additional Remarks

- **Best Practices:**  
  - Always validate your configuration.
  - Monitor with `GenerateReport()` for diagnostics.
  - Use platform-specific tuning (e.g., keep-alive) for optimal performance.
  - Clean up all listeners and workers gracefully on shutdown for robustness.

---
