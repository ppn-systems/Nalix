# Protocol

`Protocol` is the base abstraction that `TcpListenerBase` calls for connection acceptance and per-message processing. It centralizes accepting-state, connection validation, post-processing, auto-disconnect policy, error counting, and a small runtime report surface.

!!! tip "Keep protocols thin"
    A good protocol mostly accepts traffic, starts receive flow, and forwards messages into dispatch.
    If protocol code starts owning business policy, handlers and middleware usually become harder to reason about.

## Flow overview

```mermaid
flowchart LR
    A["TcpListenerBase"] --> B["OnAccept(connection)"]
    B --> C{"ValidateConnection"}
    C -->|pass| D["BeginReceive"]
    C -->|fail| E["Close connection"]
    D --> F["ProcessMessage"]
    F --> G["PostProcessMessage"]
```

## Source mapping

- `src/Nalix.Network/Protocols/Protocol.Core.cs`
- `src/Nalix.Network/Protocols/Protocol.PublicMethods.cs`
- `src/Nalix.Network/Protocols/Protocol.Lifecycle.cs`
- `src/Nalix.Network/Protocols/Protocol.Metrics.cs`

## Required contract

Derived types must implement:

```csharp
public abstract void ProcessMessage(object sender, IConnectEventArgs args);
```

This is the main per-message handler in the connection event pipeline.

## Public members at a glance

| Type | Public members |
|---|---|
| `Protocol` | `KeepConnectionOpen`, `OnAccept(...)`, `ProcessMessage(...)`, `PostProcessMessage(...)`, `ValidateConnection(...)`, `SetConnectionAcceptance(...)`, `GenerateReport()` |

## Acceptance flow

`OnAccept(connection, ct)` currently:

- rejects immediately if `IsAccepting` is false
- validates null, disposed, and cancellation state
- calls `ValidateConnection(connection)`
- if validation passes, starts `connection.TCP.BeginReceive(ct)`
- if validation fails, closes the connection
- on unexpected errors, calls `OnConnectionError(...)` and disconnects

### Failure modes worth knowing

- rejecting a connection in `ValidateConnection(...)` closes the socket immediately
- turning off acceptance with `SetConnectionAcceptance(false)` makes `OnAccept(...)` reject new sessions
- letting `ProcessMessage(...)` throw counts as a protocol error and usually triggers disconnect behavior downstream

## Post-process flow

`PostProcessMessage(sender, args)`:

- calls `OnPostProcess(args)`
- increments `TotalMessages`
- disconnects the connection when `KeepConnectionOpen` is false
- on exceptions, increments `TotalErrors`, calls `OnConnectionError(...)`, and disconnects

`KeepConnectionOpen` is backed by an atomic field and defaults to `false`.

### Common pitfalls

- doing packet dispatch work in `OnAccept(...)` instead of `ProcessMessage(...)`
- forgetting that `KeepConnectionOpen = false` will disconnect after post-processing
- ignoring `GenerateReport()` when you are debugging disconnect loops or protocol errors

## Extensibility points

- `ValidateConnection(IConnection)` for pre-receive admission checks
- `OnPostProcess(IConnectEventArgs)` for after-handler logic
- `OnConnectionError(IConnection, Exception)` for protocol-level error handling
- `Dispose(bool)` for releasing derived resources

## Operational controls

- `SetConnectionAcceptance(bool)` toggles whether new connections are accepted.
- `IsAccepting` is stored atomically.
- `Dispose()` marks the protocol disposed and suppresses finalization.

## Basic usage

```csharp
protocol.SetConnectionAcceptance(true);
await protocol.OnAccept(connection, ct);
```

Typical flow:

1. listener accepts the socket
2. protocol validates the connection
3. incoming frames are processed
4. post-processing decides whether the session stays open

## Diagnostics

`GenerateReport()` includes:

- disposed flag
- `TotalMessages`
- `TotalErrors`
- `IsAccepting`
- `KeepConnectionOpen`

## Related APIs

- [Tcp Listener](./tcp-listener.md)
- [Connection](./connection/connection.md)
- [Connection Contracts](../common/connection-contracts.md)
- [Packet Dispatch](../runtime/routing/packet-dispatch.md)
