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

## IConnection

`IConnection` is the shared connection contract.

It exposes:

- connection identity
- endpoint information
- connection metrics such as uptime and bytes sent
- crypto state such as `Secret` and `Algorithm`
- lifecycle events
- close and disconnect operations

## IConnectionHub

`IConnectionHub` is the shared connection registry contract.

It supports:

- lookup by ID
- register and unregister
- listing active connections
- association helpers such as username binding
- close-all operations

## IProtocol

`IProtocol` is the shared protocol contract.

It supports:

- `OnAccept(...)`
- `ProcessMessage(...)`
- `PostProcessMessage(...)`
- `KeepConnectionOpen`

## Example

```csharp
IConnection connection = hub.GetConnection(connectionId);
await connection.TCP.SendAsync(packet, ct);
// Transport failures now surface as exceptions.

IProtocol protocol = new SampleProtocol();
await protocol.OnAccept(connection, ct);
```

## Related APIs

- [Connection](../network/connection/connection.md)
- [Connection Hub](../network/connection/connection-hub.md)
- [Protocol](../network/runtime/protocol.md)
