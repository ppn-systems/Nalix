# Connection & IConnection — Socket Connection and Transport for .NET Servers

`Connection` is the default implementation of `IConnection`: it represents a single client connection with TCP (and optional UDP) transport, identity, and session state.

- **Namespace (impl):** `Nalix.Network.Connections`
- **Interface:** `Nalix.Common.Networking.Abstractions.IConnection`

---

## Key Concepts

- **Identity:** `ID` (Snowflake), `EndPoint` (remote address).
- **Transport:** `TCP` (required) and `UDP` (optional, lazy via `GetOrCreateUDP()`).
- **Session:** `Secret`, `Algorithm` (CipherSuiteType) for encrypt/decrypt; `BytesSent`, `UpTime`, `LastPingTime`, `ErrorCount`.

---

## IConnection at a Glance

| Member                                              | Description                                                                                 |
|-----------------------------------------------------|---------------------------------------------------------------------------------------------|
| `ID`                                                | Unique connection identifier (Snowflake).                                                   |
| `EndPoint`                                          | Remote endpoint key.                                                                        |
| `TCP`                                               | TCP transport: `SendAsync(IPacket)`, `SendAsync(ReadOnlyMemory<byte>)`, `BeginReceive(ct)`. |
| `UDP`                                               | UDP transport (may be null until `GetOrCreateUDP()`).                                       |
| `Secret`                                            | Session key for encryption (set after auth).                                                |
| `Algorithm`                                         | Cipher suite for this connection.                                                           |
| `BytesSent`, `UpTime`, `LastPingTime`, `ErrorCount` | Stats and health.                                                                           |

---

## Sending Data

- **TCP:** `connection.TCP.SendAsync(packet, ct)` or `SendAsync(memory, ct)`.
- **Control/fail:** Connection also supports sending protocol control (e.g. fail) via the protocol layer; see server protocol and middleware docs.

---

## Lifecycle

- Created when the listener accepts a socket; registered with `ConnectionHub` (or your own manager) for lookup by ID/username.
- `BeginReceive` starts the receive loop; incoming data is dispatched via protocol and `PacketDispatchChannel`.
- Dispose when the connection closes; the implementation handles cleanup and pool return where used.

---

## See Also

- [ConnectionHub](./ConnectionHub.md) — Register, lookup, broadcast connections.
- [TcpListenerBase](../Listeners/TcpListenerBase.md) — Listener and accept flow.
- [Packet Dispatch & Handler](../Routing/PacketDispatchChannel.md) — How packets are handled per connection.
