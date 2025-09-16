# Connection Class Documentation

## Overview

The `Connection` class (namespace: `Nalix.Network.Connection`) is a high-level abstraction for managing network connections in .NET applications. It handles socket communication, event-driven messaging, encryption, protocol switching (TCP/UDP), and thread-safe resource management. This component is designed for scalable, high-performance, and secure server or client networking scenarios.

---

## Functional Summary

- **Socket Management:** Wraps and manages a socket via a framed channel for robust packetized communication.
- **Transport Support:** Exposes both TCP and UDP transport interfaces for flexible data transmission.
- **Encryption:** Supports symmetric encryption with configurable algorithms and secure key management.
- **Event Handling:** Provides strongly-typed, thread-safe events for connection lifecycle and data processing.
- **Resource Pooling:** Integrates with object pools for UDP transport and buffer management to reduce allocations.
- **Thread Safety:** Uses internal locks and atomic operations for safe concurrent access.

---

## Detailed Code Explanation

### Fields

- `_cstream`: The underlying framed socket channel for reading/writing packets.
- `_lock`: Synchronization object for internal thread safety.
- `_disposed`: Tracks whether the connection has been disposed.
- `_encryptionKey`: The key used for symmetric encryption (must be 32 bytes).
- `_closeSignaled`: Atomic flag to prevent duplicate close events.
- Event fields (`_onCloseEvent`, `_onProcessEvent`, `_onPostProcessEvent`): Store event handlers.

### Constructor

- **Connection(Socket socket):**
  - Initializes the connection with the provided socket.
  - Sets up the framed channel, event callbacks, and transport instances.
  - Registers the connection with the UDP transport pool and logger.

### Properties

- `ID`: Unique identifier for the connection session.
- `TCP`: TCP transport interface for reliable data transfer.
- `UDP`: UDP transport interface for datagram communication.
- `RemoteEndPoint`: The remote network endpoint (IP/port).
- `UpTime`: Connection uptime in milliseconds.
- `IncomingPacket`: Retrieves the next incoming packet (buffer lease).
- `LastPingTime`: Timestamp of the last received ping.
- `Level`: Permission level associated with the connection.
- `Encryption`: Selected symmetric encryption algorithm.
- `EncryptionKey`: 32-byte key for encryption; validated on set.

### Events

- `OnCloseEvent`: Raised when the connection is closed.
- `OnProcessEvent`: Raised when data is processed.
- `OnPostProcessEvent`: Raised after post-processing of data.

### Methods

- **InjectIncoming(byte[] bytes):**
  - Injects incoming data (mainly used for internal/server-side simulation).
- **Close(bool force = false):**
  - Initiates a graceful connection close and triggers the `OnCloseEvent`.
- **Disconnect(string? reason = null):**
  - Forcefully disconnects the connection.
- **Dispose():**
  - Disposes of all resources, closes the connection, and returns UDP transport to the pool.

### Event Bridges (Private)

- Internal bridge methods to safely invoke events and prevent duplicate event signaling.

---

## Usage

```csharp
// Create a connection from an accepted socket
var connection = new Connection(acceptedSocket);

// Set encryption key (must be 32 bytes)
connection.EncryptionKey = my32ByteKey;

// Subscribe to events
connection.OnCloseEvent += (sender, e) => { /* handle closed connection */ };

// Send/receive data using TCP and UDP
connection.TCP.Send("Welcome!");
connection.UDP.Send(myPacket);

// Close the connection when done
connection.Close();
```

---

## Example

```csharp
// Handling incoming connections in a server
TcpListener listener = new TcpListener(IPAddress.Any, 12345);
listener.Start();

while (true)
{
    Socket client = await listener.AcceptSocketAsync();
    var con = new Connection(client);

    con.OnCloseEvent += (s, e) => Console.WriteLine($"Connection closed: {e.Connection.ID}");
    con.TCP.Send("Hello, client!");

    // ... perform more operations ...
}
```

---

## Notes & Security

- **Encryption:** Always set a secure, random 32-byte key before sending sensitive data.
- **Thread Safety:** All public methods are thread-safe, but always dispose of the connection when no longer needed.
- **Resource Management:** UDP transports are pooled; always return to the pool using `Dispose()`.
- **Event Handling:** Use events for lifecycle management and to respond to incoming or closed connections.
- **Performance:** Uses buffer leasing, object pooling, and aggressive inlining for optimal performance.
- **Error Handling:** Exceptions are logged and managed internally to avoid application crashes.

---

## SOLID & DDD Principles

- **Single Responsibility:** Each method and property has a clear, focused responsibility.
- **Open/Closed:** Adding new transports or encryption algorithms does not require changes to core logic.
- **Liskov Substitution:** Implements `IConnection` and can be replaced or extended as needed.
- **Interface Segregation:** Exposes only relevant connection operations; transport logic is separated.
- **Dependency Inversion:** Uses interfaces (`IConnection`, `ITcp`, `IUdp`, `ILogger`) for extensibility.

**Domain-Driven Design:**  
The connection logic is separated from domain entities. Networking concerns are encapsulated in infrastructure classes, keeping business/domain models clean.

---

## Additional Remarks

- **Integration:** Designed for easy use with dependency injection and logging frameworks.
- **Diagnostics:** Extensive debug and trace logging available in DEBUG builds.
- **Extensibility:** Supports extension via partial classes and interface implementations.
- **Best Practices:** Always handle events and exceptions to maintain robust, maintainable code.

---
