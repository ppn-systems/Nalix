# TcpSession

`TcpSession` is the concrete TCP client transport in `Nalix.SDK`. It uses `TransportSession` as the shared abstraction for connect/disconnect, packet sending, and receive events.

## Source mapping

- `src/Nalix.SDK/Transport/TransportSession.cs`
- `src/Nalix.SDK/Transport/TcpSession.cs`
- `src/Nalix.SDK/Transport/Internal/FRAME_READER.cs`
- `src/Nalix.SDK/Transport/Internal/FRAME_SENDER.cs`

## What it does

- connects a TCP socket to the configured server endpoint
- serializes packets into framed payloads
- reuses shared packet framing helpers for encrypt/compress and decrypt/decompress flows
- raises sync and async message events
- exposes `OnConnected`, `OnDisconnected`, `OnMessageReceived`, and `OnError`
- uses a background receive loop to process incoming frames

## Class shape

| Type | Purpose |
| --- | --- |
| `TransportSession` | Abstract base transport contract shared by client implementations. |
| `TcpSession` | Concrete TCP implementation with connect, disconnect, send, and receive support. |

## Basic usage

```csharp
TransportOptions options = ConfigurationManager.Instance.Get<TransportOptions>();
TcpSession client = new(options, catalog);

client.OnMessageReceived += (_, lease) =>
{
    using (lease)
    {
        // Inspect the received buffer here.
    }
};

await client.ConnectAsync(options.Address, options.Port);
await client.SendAsync(myPacket);
await client.DisconnectAsync();
client.Dispose();
```

## Events

- `OnConnected` is raised after a successful socket connect.
- `OnDisconnected` is raised when the session tears down or encounters a transport error.
- `OnMessageReceived` receives a leased buffer for each complete frame.
- `OnMessageAsync` allows for asynchronous processing of received raw binary frames.
- `OnError` reports connection or transport faults.

## Notes

- `TcpSession` is the current concrete client transport in the source tree.
- `TransportOptions` and `IPacketRegistry` are required at construction time.
- `SendAsync(IPacket)` serializes the packet and delegates framing and send to the internal sender pipeline.
- the receive path uses shared packet transform helpers before the completed frame is surfaced to the application layer.

## Related APIs

- [SDK Overview](./index.md)
- [Transport Session](./transport-session.md)
- [Request Options](./options/request-options.md)
- [Transport Options](./options/transport-options.md)
