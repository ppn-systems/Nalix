# Connection & IConnection — Socket Connection and Transport for Nalix.Network

`Connection` is the default `IConnection` implementation. It wraps the low-level socket, framing helpers, transport adapters, encryption state, and lifecycle events that the server relies on to dispatch packets, manage stats, and drive middleware.

- **Namespace (impl):** `Nalix.Network.Connections`
- **Interface:** `Nalix.Common.Networking.Abstractions.IConnection`

---

## Key concepts

- **Identity:** `ID` (Snowflake) uniquely identifies each connection and `NetworkEndpoint` captures the remote address/port.
- **Transport:** `TCP` is always available, and `GetOrCreateUDP(ref IPEndPoint)` lazily provisions a pooled `UdpTransport` instance when UDP messaging is required.
- **Session/verifier:** `Secret` + `Algorithm` store the negotiated cipher suite (default `CHACHA20_POLY1305`). Use `PermissionLevel` to gate access.
- **Stats:** `BytesSent`, `UpTime`, `LastPingTime`, and `ErrorCount` report health, while `TCP` exposes raw send/receive helpers used throughout the dispatch pipeline.
- **Events:** `OnProcessEvent`, `OnPostProcessEvent`, and `OnCloseEvent` mirror the packet handling stages; they are invoked from the internal `FramedSocketConnection` bridges and respect the event callbacks registered on the connection.

---

## Event hooks & lifecycle

- `OnProcessEvent` fires while a packet is being processed, allowing middleware/loggers to peek at the active payload or connection state.
- `OnPostProcessEvent` occurs after the handler completes, even if an error is raised; use it for cleanup or final auditing.
- `OnCloseEvent` is triggered once when the connection shuts down (the internal `_closeSignaled` guard prevents duplicate calls).
- `Close(force = false)` / `Disconnect(reason)` cascade to the framed socket and fire the close bridge, while `Dispose()` gracefully tears down TCP/UDP helpers, releases the UDP transport back to the pool, and suppresses finalization.

The connection also exposes `IncrementErrorCount()` and logs errors via the shared `ILogger` whenever `FramedSocketConnection` signals issues.

---

## Transmission helpers

| Member | Description |
|--------|-------------|
| `TCP` | Primary transport for framing, `SendAsync(IPacket)`, `SendAsync(ReadOnlyMemory<byte>)`, and `BeginReceive`.
| `UDP` | Lazily created by `GetOrCreateUDP`; ideal for low-latency control or telemetry frames.
| `Secret` / `Algorithm` | Use to encrypt/decrypt payloads via `TcpSession` handshake, directives, or middleware.
| `BytesSent`, `ErrorCount` | Observability used by monitoring surfaces (e.g., `ConnectionHub.GenerateReport`). |

`ConnectionExtensions.SendAsync(ControlType, ...)` builds a `Directive`/`Control` frame into a pooled buffer and transmits it over TCP. This helper logs send failures and reuses the shared `ObjectPoolManager`.

---

## Extension points

Implement custom behavior through:

- **Middleware** in the dispatch pipeline (`PacketDispatchChannel`). The connection simply exposes the events that the dispatcher attaches to.
- **Protocol-level hooks**: register handlers for the `OnProcessEvent` and `OnPostProcessEvent` if you need to respond to per-packet signals (e.g., custom logging or throttling records) before the dispatcher or after the handler finishes.
- **Control directives**: call `connection.SendAsync(ControlType.THROTTLE, ...)` or any directive via `ConnectionExtensions` to reply with status, fail frames, or disconnect instructions.

---

## See also

- [ConnectionHub](./ConnectionHub.md) — Sharded manager that tracks these `Connection` instances.
- [Listeners/TcpListenerBase](../Listeners/TcpListenerBase.md) — Where connections are accepted and the socket lifecycle begins.
- [Routing/PacketDispatchChannel](../Routing/PacketDispatchChannel.md) — How packets traverse middleware and handlers per connection.
