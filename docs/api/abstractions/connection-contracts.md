# Connection Contracts

**Namespace:** `Nalix.Abstractions.Networking`
**Assembly:** `Nalix.Abstractions`

`Nalix.Abstractions.Networking` defines the contracts shared by the network runtime and higher-level application code.

## Source mapping

- `src/Nalix.Abstractions/Networking/IConnection.cs`
- `src/Nalix.Abstractions/Networking/IConnection.Hub.cs`
- `src/Nalix.Abstractions/Networking/IConnection.Transmission.cs`
- `src/Nalix.Abstractions/Networking/IConnection.TrafficMetrics.cs`
- `src/Nalix.Abstractions/Networking/IProtocol.cs`

## Main types

- `IConnection`
- `IConnectionTrafficMetrics`
- `IConnectionHub`
- `IProtocol`

## Public members at a glance

| Type | Public members |
|---|---|
| `IConnection` | `IsDisposed`, `ID`, `UpTime`, `LastPingTime`, `NetworkEndpoint`, `Attributes`, `Secret`, `Level`, `Algorithm`, `IsUdpCreated`, `TCP`, `UDP`, `OnCloseEvent`, `OnProcessEvent`, `OnPostProcessEvent`, `Disconnect(...)` |
| `IConnectionTrafficMetrics` | `BytesSent`, `BytesReceived`, `IncrementBytesSent(...)`, `IncrementBytesReceived(...)` |
| `IConnectionHub` | `Count`, `ConnectionUnregistered`, `GetConnection(...)`, `RegisterConnection(...)`, `UnregisterConnection(...)`, `ListConnections(...)` |
| `IProtocol` | `KeepConnectionOpen`, `OnAccept(...)`, `ProcessMessage(...)`, `PostProcessMessage(...)` |

## IConnection

`IConnection` is the shared connection contract.

It exposes:

- connection identity
- endpoint information
- connection uptime and ping metrics
- crypto state such as `Secret` and `Algorithm`
- lifecycle events
- close and disconnect operations

Traffic byte counters (`BytesSent`, `BytesReceived`) are defined on the separate `IConnectionTrafficMetrics` interface.

### Common pitfalls

- treating `Secret` like a nullable optional when the current transport flow depends on it
- updating `Attributes` from multiple paths without coordinating ownership
- assuming `Disconnect(...)` is interchangeable in every lifecycle path

## IConnectionTrafficMetrics

`IConnectionTrafficMetrics` provides byte-level traffic counters for a connection.

It exposes:

- `BytesSent` — total bytes transmitted
- `BytesReceived` — total bytes received
- `IncrementBytesSent(...)` / `IncrementBytesReceived(...)` — atomic increment helpers

## IConnectionHub

`IConnectionHub` is the shared connection registry contract.

It supports:

- lookup by ID
- register and unregister
- listing active connections

### Common pitfalls

- keeping stale connection references after unregistering
- using the hub as a general app-state store instead of a connection registry
- assuming a connection exists without checking whether `GetConnection(...)` returned `null`

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

protocol.OnAccept(connection);
protocol.ProcessMessage(sender, args);
```

Typical flow:

1. accept a connection through the protocol
2. the listener handles frame normalization (decryption/decompression)
3. the listener forwards processed messages to `ProcessMessage`
4. send through the connection or packet sender when the handler finishes

## Related APIs

- [Connection](../network/connection/connection.md)
- [Connection Hub](../network/connection/connection-hub.md)
- [Session Contracts](./session-contracts.md)
- [Protocol](../network/protocol.md)
