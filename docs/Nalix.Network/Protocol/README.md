# Protocol Base Library — Abstract Network Protocol Framework for .NET

The `Protocol` class forms the core of a flexible, pluggable network protocol system—used as a base for all server protocol logic in your TCP service stack.  
It is designed to separate message routing, session and connection lifecycle, metrics, and error management, making it easy to compose complex server protocols for modern production networking.

---

## Features

- **Abstracts protocol flow:** Standardizes how protocols handle new connections, message processing, cleanup, and errors.
- **Safe, reusable**: Always thread-safe (all state/control is internally atomic).
- **Flow control:** Accept/reject new connections dynamically (maintenance mode, conditional throttling, etc.).
- **Error handling:** Automatically logs and tracks processing errors per protocol instance.
- **Keep-alive/auto-disconnect logic:** Optionally close connections after servicing a message.
- **Extensible hooks:** Override only what you need (`OnAccept`, `OnPostProcess`, `OnConnectionError`, `ValidateConnection`, `Dispose`, etc.).
- **Metrics/reporting API**: Live counters for processed messages, error count, accepting-state, and connection handling logic.

---

## Typical Usage

Sub-class `Protocol` to implement your business logic:

```csharp
public class ChatProtocol : Protocol
{
    public override void ProcessMessage(object sender, IConnectEventArgs args)
    {
        // Parse and process incoming messages...
        // Use args.Connection and args.Message as needed
    }
    
    protected override bool ValidateConnection(IConnection connection)
    {
        // Allow/reject connection; e.g. IP filtering, auth, etc
        return base.ValidateConnection(connection);
    }

    protected override void OnPostProcess(IConnectEventArgs args)
    {
        // Optional: custom logic after message handling
        base.OnPostProcess(args);
    }
}
```

**Wiring up into your server listener:**

```csharp
var protocol = new ChatProtocol();
var listener = new TcpListenerBase(protocol);
listener.Activate();
```

---

## Key API Reference

| Method/Property                  | Purpose                                                      |
|----------------------------------|--------------------------------------------------------------|
| `ProcessMessage` (abstract)      | **Main flow:** Handle one message on a connection            |
| `OnAccept` / `ValidateConnection`| Called on new connection: validate and/or init connection    |
| `KeepConnectionOpen`             | Controls: immediate disconnect after message vs. long-lived  |
| `SetConnectionAcceptance(bool)`  | Dynamically enable/disable accepting connections             |
| `OnPostProcess`                  | Hook: after every message (success or error)                 |
| `OnConnectionError`              | Hook: after error thrown/handled, for logging/metrics        |
| `Dispose()` / `Dispose(bool)`    | Clean protocol resources (metrics, buffers, etc)             |
| `TotalMessages` / `TotalErrors`  | Metrics counters                                             |
| `GenerateReport()`               | Live diagnostic protocol state dump                          |

---

## Metrics/Diagnostics Example

Call `protocol.GenerateReport()` for a quick status snapshot:

```log
[2026-03-12 14:20:00] Protocol Status:
--------------------------------------------
Is Disposed             : 0
Total Messages          : 5932
Total Errors            : 3
Is Accepting            : True
Keep Connections Open   : False
--------------------------------------------
```

---

## Extending / Best Practices

- **Override only what you need:** Default logic is safe for most use; override for custom behaviors.
- **Connection validation:** Use `ValidateConnection` for IP filtering, maintenance, health checks, initial protocol negotiation, TLS handshakes, etc.
- **Graceful Error Handling:** Use `OnConnectionError` or override `PostProcessMessage` for custom session clean-ups or business error handling.
- **Metrics & Maintenance:** Use `SetConnectionAcceptance(false)` for maintenance, keep accepting-state visible to orchestration.
- **Auto-Close:** Set `KeepConnectionOpen = false` to auto-disconnect connections unless you need long-lived sessions.

---

## Use Cases

- Quickly scaffold any custom TCP/IP protocol handler for production server
- Employ in gaming servers, chat/messaging, IoT ingress, binary command services, etc
- Write test protocols for integration, fuzz, or load-test servers (swap in place)

---

## License

Licensed under the Apache License, Version 2.0.  
Copyright (c) 2025-2026 PPN Corporation.

---

## See Also

- [TcpListenerBase](../Listeners/TcpListenerBase.md)
- [Nalix Server Stack Reference](../README.md)
