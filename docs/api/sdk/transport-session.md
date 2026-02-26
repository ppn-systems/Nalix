# TransportSession

`TransportSession` is the abstract client transport contract in `Nalix.SDK`. It defines the shared lifecycle and event surface for concrete transports such as `TcpSession`.
It is transport-focused and does not own packet-specific policy; packet transforms are handled by the concrete session flow and shared frame helpers.

## Source mapping

- `src/Nalix.SDK/Transport/TransportSession.cs`

## What it exposes

- transport configuration through `Options`
- the packet registry through `Catalog`
- connection state through `IsConnected`
- the protocol handler through `Protocol`
- lifecycle events: `OnConnected`, `OnDisconnected`, `OnMessageReceived`, and `OnError`
- transport operations: `ConnectAsync`, `DisconnectAsync`, `SendAsync`, and `Dispose`

## Why it matters

Use this type when you want code that can depend on the client transport contract without binding to the concrete TCP implementation.

## Related APIs

- [TCP Session](./tcp-session.md)
- [SDK Overview](./index.md)
