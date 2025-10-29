# ReliableClient — Robust, Auto-Reconnecting TCP Client for .NET

**ReliableClient** is a robust TCP client designed for applications that require high reliability, live monitoring, and production-grade keep-alive semantics.  
It manages low-level socket, framing, and concurrency concerns, and exposes a simple high-level .NET API for packet send/receive, auto-reconnect, bandwidth statistics, and connection health.

- **Namespace:** `Nalix.SDK.Transport`
- **Class:** `ReliableClient` (sealed)
- **Implements:** `IClientConnection` (thread-safe)

---

## Features

- **Thread-safe**: safe for concurrent calls to `SendAsync()`, `ConnectAsync()`, `DisconnectAsync()`, and `Dispose()`
- **Automatic reconnection**: with exponential backoff and maximum attempt control
- **Heartbeat (keep-alive)**: periodic CONTROL PING/PONG packets to check liveness and measure RTT
- **Bandwidth measurement**: tracks send/receive Bps in real-time (sampling interval: 1s)
- **Framing protocol**: manages packetization and headers, supporting Protocol-specific-only design
- **TaskManager workers**: all background receive, heartbeat, and bandwidth sampling use scalable task-managed loops that survive reconnects
- **Event-driven API**: hooks for `OnConnected`, `OnDisconnected`, `OnMessageReceived`, `OnError`, etc.
- **Async-first**: full `Task`-based support for non-blocking operation
- **Graceful cleanup**: supports full disposal, and will never leak background workers

---

## API Usage

### Basic Connect/Send/Receive

```csharp
var client = new ReliableClient();
client.OnConnected += ...;
client.OnMessageReceived += (sender, buf) =>
{
    // use/read buf here; do NOT forget to Dispose() if you take ownership!
};

await client.ConnectAsync("host.example.com", 12345);
await client.SendAsync(somePacket); // IPacket serializable

await client.DisconnectAsync();
client.Dispose();
```

---

### Key Properties

| Property                | Description                                 |
|-------------------------|---------------------------------------------|
| `IsConnected`           | true if connected and socket alive          |
| `BytesSent`             | cumulative bytes sent                       |
| `BytesReceived`         | cumulative bytes received                   |
| `SendBytesPerSecond`    | bytes/sec send rate (sampled, 1s window)    |
| `ReceiveBytesPerSecond` | bytes/sec receive rate (sampled, 1s)        |
| `LastHeartbeatRtt`      | ms, latest ping-pong RTT via CONTROL        |
| `LastHeartbeatPong`     | last CONTROL-PONG packet (may be null)      |

---

## Events (Callback Style)

| Event                             | Trigger                             |
|-----------------------------------|-------------------------------------|
| `OnConnected`                     | On successful low-level connect     |
| `OnDisconnected(Exception)`       | Connection dropped or error         |
| `OnMessageReceived(IBufferLease)` | Called on every new message         |
| `OnError(Exception)`              | On fatal receive/send error         |
| `OnBytesSent(long)`               | Every successful send               |
| `OnBytesReceived(long)`           | On receive, every message           |
| `OnMessageReceivedAsync`          | Optional, for async message dispatch|

---

## Internals & Best Practice

- Heartbeats (PING, PONG) — interval and timeout are set via `TransportOptions`
- On error or disconnect, triggers full teardown & auto-reconnect (if enabled)
- `TaskManager` usage: all background operations are workers/recurring, ensuring safe shutdown even on rapid connect/disconnect cycles
- *Always* check for disposal before actions — all public methods are safe
- DNS resolution and multi-address handling: will attempt all resolved endpoints in order (first-up model)

---

## Error Handling

- Raises `OnError` and `OnDisconnected` (with cause `Exception`)
- Throws `ObjectDisposedException` if use-after-dispose
- Throws on network/DNS/invalid packets/errors; will auto-reconnect if `ReconnectEnabled`

---

## Sample: Custom Protocol

```csharp
// Register your packet registry and configuration before use!
InstanceManager.Instance.RegisterInstance<IPacketRegistry>(myRegistry);
// Set up TransportOptions in config

var client = new ReliableClient();
await client.ConnectAsync("peer.acme.net", 5000);
await client.SendAsync(new Control { Type = ControlType.PING, ... });

// Process messages and handle events
```

> **Note:** You must supply an `IPacketRegistry` (for decode) to the DI `InstanceManager` *before* using `ReliableClient`.

---

## Technical Notes

- All socket operations and background workers are resilient to spurious disconnections
- Bandwidth stats are sampled every second (accurate within 1% for normal gigabit-range use)
- Underlying socket is shutdown and disposed correctly during disconnect/dispose
- Only one background receive/heartbeat/taskmanager worker runs per connection (no duplication or race)
- Framing uses a 2-byte prefix for length, adaptable to most binary protocols

---

## Reference

- `TransportOptions` for configuration customization
- See also: `IPacketRegistry`, `Control`, `TaskManager` (injection / DI)

---

## License

Licensed under the Apache License, Version 2.0.
