# Protocol — Base class for listener-driven connection protocols

`Protocol` is the base abstraction that `TcpListenerBase` calls for connection acceptance and per-message processing. It centralizes accepting-state, connection validation, post-processing, auto-disconnect policy, error counting, and a small runtime report surface.

## Mapped sources

- `src/Nalix.Network/Protocols/Protocol.Core.cs`
- `src/Nalix.Network/Protocols/Protocol.PublicMethods.cs`
- `src/Nalix.Network/Protocols/Protocol.Lifecycle.cs`
- `src/Nalix.Network/Protocols/Protocol.Metrics.cs`

## Required contract

Derived types must implement:

```csharp
public abstract void ProcessMessage(object sender, IConnectEventArgs args);
```

This is the main per-message handler that runs from the connection event pipeline.

## Acceptance flow

`OnAccept(connection, ct)` is the entry point called by `TcpListenerBase` for a newly accepted connection.

Current behavior:

- rejects immediately if `IsAccepting` is false
- validates null/disposed/cancellation state
- calls `ValidateConnection(connection)`
- if validation passes, starts `connection.TCP.BeginReceive(ct)`
- if validation fails, closes the connection
- on unexpected errors, calls `OnConnectionError(...)` and disconnects the connection

## Post-process flow

`PostProcessMessage(sender, args)`:

- calls `OnPostProcess(args)`
- increments `TotalMessages`
- disconnects the connection when `KeepConnectionOpen` is false
- on exceptions, increments `TotalErrors`, calls `OnConnectionError(...)`, and disconnects

`KeepConnectionOpen` is backed by an atomic field and defaults to `false` unless a derived protocol changes it.

## Extensibility points

- `ValidateConnection(IConnection)` for pre-receive admission checks
- `OnPostProcess(IConnectEventArgs)` for after-handler logic
- `OnConnectionError(IConnection, Exception)` for protocol-level error handling
- `Dispose(bool)` for releasing derived resources

## Operational controls

- `SetConnectionAcceptance(bool)` toggles whether new connections are accepted.
- `IsAccepting` is stored atomically.
- `Dispose()` marks the protocol disposed and suppresses finalization.

## Diagnostics

`GenerateReport()` includes:

- disposed flag
- `TotalMessages`
- `TotalErrors`
- `IsAccepting`
- `KeepConnectionOpen`

## Usage sketch

```csharp
public sealed class ChatProtocol : Protocol
{
    public override void ProcessMessage(object sender, IConnectEventArgs args)
    {
        // Handle one message
    }

    protected override bool ValidateConnection(IConnection connection) => true;
}
```

## See also

- [TcpListenerBase](../Listeners/TcpListenerBase.md)
- [Connection](../Connections/Connection.md)
