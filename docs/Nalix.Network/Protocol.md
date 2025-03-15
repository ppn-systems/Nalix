# Protocol Base Class Documentation

## Overview

The `Protocol` class (namespace: `Nalix.Network.Protocols`) is an abstract base class for implementing custom network protocols in .NET applications. It provides the core logic for connection lifecycle management, message processing, error handling, and protocol diagnostics. Designed with SOLID and Domain-Driven Design (DDD) principles in mind, it serves as a robust and extensible foundation for building efficient, maintainable, and secure protocol handlers.

---

## Functional Summary

- **Connection Management:** Handles accepting, validating, and closing network connections.
- **Message Processing:** Defines the main entry point for processing messages (`ProcessMessage`) and supports post-processing hooks.
- **Error Handling:** Centralized error tracking and customizable error response logic.
- **Lifecycle Management:** Supports disposal and state transitions for protocol instances.
- **Thread Safety:** Uses atomic operations for connection acceptance and keep-alive settings.
- **Diagnostics:** Provides metrics (total messages, total errors) and generates human-readable protocol reports.

---

## Detailed Code Explanation

### Core Methods

- **`ProcessMessage(object? sender, IConnectEventArgs args)`:**  
  Abstract method. Must be implemented by derived classes to handle incoming messages. Runs when a new message is received for a connection.

- **`OnPostProcess(IConnectEventArgs args)`:**  
  Virtual hook method, called automatically after message processing for additional custom logic.

- **`PostProcessMessage(object? sender, IConnectEventArgs args)`:**  
  Called after the main handler. Calls `OnPostProcess`, optionally closes the connection if `KeepConnectionOpen` is false, and handles errors.

- **`OnAccept(IConnection connection, CancellationToken cancellationToken)`:**  
  Called when a new connection is accepted. Default implementation starts receiving data if validation passes; override for custom logic.

- **`SetConnectionAcceptance(bool isEnabled)`:**  
  Enable or disable the protocol's acceptance of new connections (e.g., for maintenance mode).

- **`OnConnectionError(IConnection connection, Exception exception)`:**  
  Virtual method to handle connection-level errors. Default increments an error counter; override for custom responses.

- **`ValidateConnection(IConnection connection)`:**  
  Virtual method to validate a connection before accepting. Default always returns `true`; override for custom validation.

- **`Dispose()`, `Dispose(bool disposing)`:**  
  Handles resource cleanup and transitions protocol to the disposed state.

### Properties

- **`KeepConnectionOpen`**:  
  Controls whether the connection stays open after processing. Thread-safe using atomic operations.

- **`IsAccepting`**:  
  Indicates if the protocol is currently accepting new connections.

- **`TotalErrors`**:  
  The number of errors encountered during protocol operation.

- **`TotalMessages`**:  
  The total number of messages processed.

---

## Usage

```csharp
public class MyProtocol : Protocol
{
    public override void ProcessMessage(object? sender, IConnectEventArgs args)
    {
        // Handle message for the connection
    }

    protected override bool ValidateConnection(IConnection connection)
    {
        // Custom validation logic
        return connection != null && connection.Level > PermissionLevel.None;
    }

    protected override void OnConnectionError(IConnection connection, Exception exception)
    {
        // Custom error handling logic
        base.OnConnectionError(connection, exception);
    }
}

// Usage in a TCP listener
var protocol = new MyProtocol();
protocol.SetConnectionAcceptance(true);
// ... pass protocol to your listener class
```

---

## Example

```csharp
public class EchoProtocol : Protocol
{
    public override void ProcessMessage(object? sender, IConnectEventArgs args)
    {
        // Simple echo logic
        var packet = args?.Message;
        args?.Connection?.TCP.Send(packet);
    }
}

// Start protocol and accept connections
var echoProtocol = new EchoProtocol();
echoProtocol.SetConnectionAcceptance(true);
```

---

## Notes & Security

- **Thread Safety:** All state changes (accepting, keep-alive, metrics) are thread-safe.
- **Error Handling:** All exceptions during post-processing are logged and trigger the error handler.
- **Connection Validation:** Always override `ValidateConnection` to check credentials, rate limits, etc., for security.
- **Disposal:** Always call `Dispose()` when the protocol is no longer needed to release resources.
- **SOLID & DDD:**  
  - Single Responsibility: Each protocol handles only its own logic.
  - Open/Closed: Extend by inheriting and overriding virtual/abstract methods.
  - Dependency Inversion: Depends on abstractions (`IConnection`, `ILogger`).

---

## SOLID & DDD Principles

- **Single Responsibility:** Each method/class has a focused responsibility.
- **Open/Closed:** New protocol behaviors added via subclassing, not by modifying the base.
- **Liskov Substitution:** All subclasses can be used wherever `Protocol` is expected.
- **Interface Segregation:** Only exposes relevant protocol operations.
- **Dependency Inversion:** Uses interfaces for connections and logging.

**Domain-Driven Design:**  
Protocol logic is infrastructure and should be kept separate from core domain logic. Use domain events and services for business rules.

---

## Additional Remarks

- **Diagnostics:** Use `GenerateReport()` for real-time protocol health and status.
- **Integration:** Designed for easy use with dependency injection and logging frameworks.
- **Extensibility:** Ideal for implementing custom protocols (game, chat, RPC, etc.) in scalable .NET servers.

---
