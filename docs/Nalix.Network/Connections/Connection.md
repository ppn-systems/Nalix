# Connection — Default `IConnection` implementation

`Connection` is the concrete transport/session object used by Nalix.Network after a socket is accepted. It wraps the framed socket transport, owns the connection identity and endpoint, exposes TCP/UDP adapters, and bridges low-level transport callbacks into the higher-level connection events used by listeners, protocols, and dispatch code.

## Mapped sources

- `src/Nalix.Network/Connections/Connection.cs`
- `src/Nalix.Network/Connections/Connection.Extensions.cs`
- `src/Nalix.Network/Connections/Connection.Transmission.cs`
- `src/Nalix.Network/Connections/Connection.EventArgs.cs`
- `src/Nalix.Network/Connections/Connection.Endpoint.cs`

## Core state

| Member | Meaning |
|---|---|
| `ID` | Snowflake session ID created at construction time. |
| `NetworkEndpoint` | Remote endpoint resolved from the accepted socket. |
| `TCP` | Always-present TCP transport facade backed by `FramedSocketConnection`. |
| `UDP` | UDP transport facade when provisioned by the connection. |
| `Secret` | Session secret / keying material. |
| `Algorithm` | Current cipher suite, defaulting to `CHACHA20_POLY1305`. |
| `Level` | Permission level for authorization-sensitive handlers. |
| `BytesSent` | Total transmitted bytes, read atomically. |
| `ErrorCount` | Number of transport / dispatch errors recorded for this connection. |
| `UpTime`, `LastPingTime` | Metrics exposed through the framed socket cache. |

## Event bridges

The connection exposes three events:

- `OnCloseEvent`
- `OnProcessEvent`
- `OnPostProcessEvent`

Those are not raised directly by application code. Instead, `FramedSocketConnection.SetCallback(...)` wires them to bridge methods inside `Connection`:

- close callbacks go through `AsyncCallback.InvokeHighPriority(...)`
- process/post-process callbacks go through `AsyncCallback.Invoke(...)`

`_closeSignaled` ensures the close event is emitted only once.

## Lifecycle

- Construction creates the session ID, resolves the remote endpoint, creates `ConnectionEventArgs`, and initializes `FramedSocketConnection`.
- `Close(force = false)` forwards to the close bridge.
- `Disconnect(reason)` is currently an alias of `Close(force: true)`.
- `Dispose()` marks the object disposed, disconnects, disposes the framed socket, and returns any pooled UDP transport to `ObjectPoolManager`.

## Protocol and dispatch integration

- `TcpListenerBase` subscribes `OnCloseEvent`, `OnProcessEvent`, and `OnPostProcessEvent` when the connection is created.
- `Protocol.ProcessMessage` and `Protocol.PostProcessMessage` are attached to the process events.
- `ConnectionLimiter.OnConnectionClosed` is typically attached to `OnCloseEvent` so per-IP counts remain correct.

## Directive sending helper

`ConnectionExtensions.SendAsync(...)` sends a protocol `Directive` over the TCP transport.

Current behavior:

- rents a pooled `Directive`
- serializes using a stack/small-path or rented `BufferLease` depending on size
- sends through `connection.TCP.SendAsync(...)`
- logs failures, then returns the directive to the pool

Use this helper for control replies such as throttle/fail/timeout/network directives.

## Notes

- The close path bypasses normal callback backpressure by using the high-priority callback invoker.
- The constructor requires a live `Socket`; `NetworkEndpoint` is captured immediately from `socket.RemoteEndPoint`.
- `BytesSent` is exposed as an atomic read, so monitoring code can sample it safely.

## See also

- [ConnectionHub](./ConnectionHub.md)
- [TcpListenerBase](../Listeners/TcpListenerBase.md)
- [PacketDispatchChannel](../Routing/PacketDispatchChannel.md)
