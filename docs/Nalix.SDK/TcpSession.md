# TcpSessionBase, TcpSession & IoTTcpSession — Reliable TCP Sessions for .NET

`TcpSessionBase` peers with `TcpSession` and `IoTTcpSession` to provide the Nalix.SDK client transport stack. The base class owns framing, buffer management, event wiring, and the send/receive helpers, while the derived sessions layer on heartbeat, reconnect, bandwidth monitoring, and IoT-friendly scheduling.

- **Namespace:** `Nalix.SDK.Transport`
- **Base class:** `TcpSessionBase` (abstract)
- **Derived classes:** `TcpSession`, `IoTTcpSession`

---

## Class hierarchy at a glance

| Class | Responsibility |
|-------|----------------|
| `TcpSessionBase` | Shared plumbing: options (`TransportOptions`, `IPacketRegistry`), framing helpers, `SendAsync` overloads (packet, bytes, encrypt/compress), lifecycle state, and events (`OnConnected`, `OnDisconnected`, `OnError`, `OnBytesSent`, `OnBytesReceived`, `OnMessageReceived`, `OnReconnected`). |
| `TcpSession` | Production-ready reliable client with TaskManager receive worker, `SessionMonitor` (heartbeat + rate sampler), exponential reconnect loop, and detailed bandwidth counters. |
| `IoTTcpSession` | Lightweight IoT client: serializes connect calls with `_connectLock`, runs receive loop via `Task.Run`, shares the same framing code but keeps the session simple for constrained runtime environments. |

---

## Core features

- **Automatic reconnect:** exponential backoff + jitter, cancellable in-flight attempts, configurable via `TransportOptions.Reconnect*`.
- **Heartbeat + monitoring (`TcpSession` only):** `SessionMonitor` manages keep-alive PING/PONG (via `Control` frames), rate sampling (Bps), and optional time sync hooks.
- **Advanced send paths:** pooling via `BufferLease`, compression & encryption toggles, and a helper that bundles serialization → optional compress/encrypt → framed send, keeping the hot path GC-free.
- **Thread-safe events:** `OnMessageReceived`/`OnMessageReceivedAsync` dispatch helpers own message buffers and avoid leaking leases; asynchronous handlers run through `InlineDispatcher` / `TaskManager` as configured.
- **State + diagnostics:** `TcpSessionState` exposes `Connecting`, `Connected`, `Reconnecting`, `Disconnected`, `Disposed`; `BytesSent/BytesReceived`, plus `SendBytesPerSecond/ReceiveBytesPerSecond` counters (updated by the monitor sampler).
- **Graceful teardown:** `DisconnectAsync`, `Dispose`, `DisposeAsync`, and `TearDownConnection` ensure sender/receiver cleanup, socket shutdown, and monitor disposal.

---

## TransportOptions snapshot

| Setting | Purpose |
|---------|---------|
| `Address`, `Port`, `ConnectTimeoutMillis` | Where to connect and how long to wait per attempt. |
| `ReconnectEnabled`, `ReconnectMaxAttempts`, `ReconnectBase/MaxDelayMillis` | Drive the reconnect loop. `ReconnectMaxAttempts = 0` means unlimited, delays double until `ReconnectMaxDelayMillis`. |
| `KeepAliveIntervalMillis` | Heartbeat interval (0 disables) for `TcpSession`. |
| `BufferSize`, `NoDelay`, `MaxPacketSize` | Socket tuning. |
| `EnableCompression`, `MinSizeToCompress` | Compress before encryption if the payload is large enough. |
| `Secret`, `Algorithm` | Session key (set after handshake) and cipher suite (e.g., `CHACHA20_POLY1305`). |

> Call `Options.Validate()` (already invoked by the `TcpSession` constructor) before using the client.

---

## Lifecycle & reconnect flow

1. `ConnectAsync(host, port)` resolves DNS, configures the socket, and wires the framing helpers.
2. The session replaces the state with `Connecting`, then fires `OnConnected` once the socket is live (`TcpSession` also fires `OnReconnected` after the first reconnect).
3. `StartReceiveWorker` begins the receive loop:
   - `TcpSession`: TaskManager worker + `SessionMonitor` (rate sampler + heartbeat loop).
   - `IoTTcpSession`: plain `Task.Run` loop, keeping dependencies minimal.
4. On send errors/disconnects, `TcpSession` triggers `HANDLE_DISCONNECT_AND_RECONNECT_ASYNC` (auto-reconnect). `IoTTcpSession` uses a simpler `ReconnectLoopAsync` guarded by `_reconnecting`.
5. `TearDownConnection` cancels CTS, disposes the sender/receiver, closes the socket, and resets state.
6. `Dispose` / `DisconnectAsync` call through `TearDownConnection` and ensure `OnDisconnected` is triggered with the error cause.

---

## Events & metrics

| Event/Property | Meaning |
|----------------|---------|
| `OnConnected` | Raised after a successful connect. |
| `OnReconnected` | Raised after an automatic reconnection (argument = attempt count). |
| `OnDisconnected(Exception)` | Notifies subscribers when the socket tears down. |
| `OnMessageReceived(IBufferLease)` | Message receive hook; handler must dispose the lease. |
| `OnMessageReceivedAsync` | Optional async handler that receives the raw bytes. |
| `OnBytesSent`, `OnBytesReceived` | Notifies every send/receive. |
| `OnError(Exception)` | Fired for send/receive errors before reconnect attempts. |
| `BytesSent`, `BytesReceived`, `SendBytesPerSecond`, `ReceiveBytesPerSecond` | Exposure of the counters updated by `SessionMonitor`. |
| `State` (`TcpSessionState`) | `Disconnected`, `Connecting`, `Connected`, `Reconnecting`, `Disposed`. |

---

## Extensions & protocol helpers

- Use the helpers in [`docs/Nalix.SDK/TcpSession-Extensions.md`](./TcpSession-Extensions.md) for fluent CONTROL builders (`NewControl`, `PingAsync`), secure handshake (`HandshakeAsync` with X25519 + SHA3), directive handling (THROTTLE, REDIRECT, NOTICE, NACK), and the request/response helpers (`RequestAsync` + `RequestOptions`).
- Extensions rely on the `IClientConnection` event system to subscribe/unsubscribe safely and dispose leases.
- `RequestOptions` controls timeout, retry count, and optional encryption; `RequestExtensions.RequestAsync` avoids races by subscribing before sending.

---

## Sample usage

```csharp
var session = new TcpSession();
session.OnMessageReceived += (sender, lease) => {
    using (lease) {
        var packet = session.Catalog.TryDeserialize(lease.Span, out var p) ? p : null;
        // handle packet
    }
};

await session.ConnectAsync("server.example", 57206);
await session.SendAsync(new Control { Type = ControlType.PING });
await session.DisconnectAsync();
session.Dispose();
```

> **Tip:** configure `TransportOptions` (via `ConfigurationManager`) before creating the session, and register an `IPacketRegistry` on `InstanceManager` for packet deserialization.

---

## See also

- [TcpSession Extensions](./TcpSession-Extensions.md)
- [Nalix.SDK README](./README.md)
- [Nalix.Network Architecture](../Nalix.Network/Architecture.md)
