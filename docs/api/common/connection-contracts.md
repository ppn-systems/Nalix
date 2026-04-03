# Connection Contracts

`Nalix.Common.Networking` defines the contracts shared by the network runtime and higher-level application code.

## Source mapping

- `src/Nalix.Common/Networking/IConnection.cs`
- `src/Nalix.Common/Networking/IConnection.Hub.cs`
- `src/Nalix.Common/Networking/IConnection.Transmission.cs`
- `src/Nalix.Common/Networking/IProtocol.cs`

## Main types

- `IConnection`
- `IConnectionHub`
- `IProtocol`

## Public members at a glance

| Type | Public members |
|---|---|
| `IConnection` | `ID`, `UpTime`, `BytesSent`, `LastPingTime`, `NetworkEndpoint`, `Attributes`, `Secret`, `Level`, `Algorithm`, `OnCloseEvent`, `OnProcessEvent`, `OnPostProcessEvent`, `Close(...)`, `Disconnect(...)` |
| `IConnectionHub` | `GetConnection`, `TryGetConnection`, `Register`, `Unregister`, `GetAll`, `CloseAll`, `BindUsername`, `TryGetUsername` |
| `IProtocol` | `KeepConnectionOpen`, `OnAccept(...)`, `ProcessMessage(...)`, `PostProcessMessage(...)` |

## IConnection

`IConnection` is the shared connection contract.

It exposes:

- connection identity
- endpoint information
- connection metrics such as uptime and bytes sent
- crypto state such as `Secret` and `Algorithm`
- lifecycle events
- close and disconnect operations

### Common pitfalls

- treating `Secret` like a nullable optional when the current transport flow depends on it
- updating `Attributes` from multiple paths without coordinating ownership
- assuming `Close(...)` and `Disconnect(...)` are interchangeable in every lifecycle path

## IConnectionHub

`IConnectionHub` is the shared connection registry contract.

It supports:

- lookup by ID
- register and unregister
- listing active connections
- association helpers such as username binding
- close-all operations

### Common pitfalls

- keeping stale connection references after unregistering
- using the hub as a general app-state store instead of a connection registry
- assuming a connection exists without checking `TryGetConnection(...)`

## IProtocol

`IProtocol` is the shared protocol contract.

It supports:

- `OnAccept(...)`
- `ProcessMessage(...)`
- `PostProcessMessage(...)`
- `KeepConnectionOpen`

### Common pitfalls

- doing business logic in `OnAccept(...)` that really belongs in dispatch or middleware
- forgetting to keep `ProcessMessage(...)` and `PostProcessMessage(...)` aligned with the connection lifecycle
- treating `KeepConnectionOpen` as a transport-level guarantee instead of a protocol decision

## Example

```csharp
IConnection connection = hub.GetConnection(connectionId);
IProtocol protocol = new SampleProtocol();

protocol.OnAccept(connection, ct);
protocol.ProcessMessage(sender, args);
```

Typical flow:

1. accept a connection through the protocol
2. let the protocol forward message events
3. send through the connection or packet sender when the handler finishes

## Related APIs

- [Connection](../network/connection/connection.md)
- [Connection Hub](../network/connection/connection-hub.md)
- [Protocol](../network/runtime/protocol.md)
