# UdpListenerBase Documentation

## Overview

The `UdpListenerBase` class (namespace: `Nalix.Network.Listeners.Udp`) is an abstract base class for building high-performance UDP network listeners in .NET. It handles asynchronous datagram reception, protocol processing, socket configuration, diagnostics, and integrates with time synchronization and dependency injection systems. It is designed to be extended for custom UDP-based servers or services.

---

## Functional Summary

- **UDP Reception:** Listens for UDP datagrams on a specified port and processes them asynchronously.
- **Protocol Integration:** Passes incoming datagrams to a user-supplied protocol handler.
- **Resource Management:** Handles activation, deactivation, and proper disposal of sockets and resources.
- **Time Synchronization:** Integrates with a time sync system for distributed environments.
- **Object Pooling:** Designed to work efficiently with pooled objects and reusable contexts.
- **Diagnostics & Reporting:** Tracks packet statistics, errors, and runtime health for monitoring.
- **Socket Configuration:** Optimizes socket options (buffer size, no delay, keep-alive, etc.) for best performance.
- **Security:** Supports authentication checks on incoming packets (via `IsAuthenticated`).

---

## Detailed Structure and Explanation

### Fields and Properties

- `_port`: The UDP port being listened on.
- `_protocol`: The protocol handler for incoming datagrams.
- `_udpClient`: The underlying `UdpClient` socket.
- `_cts`, `_cancellationToken`: Used for cooperative cancellation.
- `_isRunning`, `_isDisposed`: Track listener status.
- Diagnostics counters: `_rxPackets`, `_rxBytes`, `_dropShort`, `_dropUnauth`, `_dropUnknown`, `_recvErrors`.
- Time sync diagnostics: `_lastSyncUnixMs`, `_lastDriftMs`.
- `IsListening`: Indicates if the listener is running.
- `IsTimeSyncEnabled`: Property to enable/disable time sync (must be set when not running).

### Initialization

- **Constructor:**  
  Sets up the protocol handler, port, and time sync event handlers.
- **Initialize():**  
  Creates the `UdpClient`, configures the underlying socket, and prepares for data reception.

### Public API

- **Activate(CancellationToken):**  
  Starts listening for datagrams, spawns async receive workers, and integrates with background task manager.
- **Deactivate(CancellationToken):**  
  Stops listening and cleans up resources.
- **Dispose():**  
  Releases all resources, cancels tasks, and detaches event handlers.
- **GenerateReport():**  
  Returns a detailed diagnostic report with runtime, socket, traffic, and error stats.
- **SynchronizeTime(long milliseconds):**  
  Records and processes server time sync events; can be overridden for custom behavior.

### Internal Reception Logic

- **ReceiveDatagramsAsync(CancellationToken):**  
  Main loop for receiving datagrams asynchronously, dispatches each to a worker for processing.
- **ProcessDatagram(UdpReceiveResult):**  
  Validates, authenticates, and passes packet data to the correct connection, or logs errors and drops packets as needed.

### Socket Configuration

- **ConfigureHighPerformanceSocket(Socket):**  
  Applies performance-optimized socket options (NoDelay, buffer sizes, keep-alive).

---

## Usage

```csharp
// Subclass UdpListenerBase to implement authentication and custom logic
public class MyUdpListener : UdpListenerBase
{
    public MyUdpListener(IProtocol protocol) : base(protocol) { }

    protected override bool IsAuthenticated(IConnection connection, in UdpReceiveResult result)
    {
        // Implement your own authentication logic here
        return true;
    }

    protected override void OnTimeSynchronized(long serverMs, long localMs, long driftMs)
    {
        // Optional: handle time sync drift
    }
}

// Start listening
var listener = new MyUdpListener(new MyProtocolHandler());
listener.Activate();

// ... Later, to stop
listener.Deactivate();
listener.Dispose();
```

---

## Example

```csharp
public class EchoProtocol : IProtocol
{
    public void OnAccept(IConnection conn, CancellationToken token) { /* not used in UDP */ }
    // Implement packet handling as needed
}

public class EchoUdpListener : UdpListenerBase
{
    public EchoUdpListener() : base(new EchoProtocol()) { }

    protected override bool IsAuthenticated(IConnection conn, in UdpReceiveResult result) => true;
}

var udpListener = new EchoUdpListener();
udpListener.Activate();
```

---

## Notes & Security

- **Authentication:** Always override `IsAuthenticated` for your application to prevent unauthorized packets.
- **Resource Management:** Always call `Deactivate()` and `Dispose()` for a clean shutdown.
- **Diagnostics:** Use `GenerateReport()` for runtime monitoring and troubleshooting.
- **Thread Safety:** All operations are thread-safe and designed for concurrent environments.
- **Performance:** Uses background workers, pooling, and efficient socket options for high throughput.
- **Configuration:** Socket settings are loaded from `NetworkSocketOptions` for environment-specific tuning.

---

## SOLID & DDD Principles

- **Single Responsibility:** Each method and class handles a focused concern (socket setup, receive loop, reporting, etc.).
- **Open/Closed:** Easily extended for new protocols and authentication strategies.
- **Liskov Substitution:** Can be subclassed and replaced with custom implementations.
- **Interface Segregation:** Implements only necessary interfaces and keeps concerns separated.
- **Dependency Inversion:** Uses protocol and logger abstractions for flexibility and testability.

**Domain-Driven Design:**  
All low-level networking and infrastructure logic is separated from domain-level application logic, keeping your business code clean and maintainable.

---

## Additional Remarks

- **Best Practices:**  
  - Enable time synchronization if needed for distributed systems.
  - Monitor and tune buffer sizes and concurrency for your workload.
  - Handle all exceptions and log errors for robust production deployments.
  - Customize reporting for your operational needs.

---
