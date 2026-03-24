# Nalix.SDK — Client Transport and Extensions

**Nalix.SDK** provides client-side transport (TCP/UDP), auto-reconnect, protocol extensions, and optional localization. It is intended for .NET applications that connect to Nalix.Network (or compatible) servers.

---

## Module Summary

| Component            | Description                                                                                          |
|----------------------|------------------------------------------------------------------------------------------------------|
| **TcpSessionBase / TcpSession / IoTTcpSession** | Reliable TCP session stack: shared base plumbing, TcpSession (auto-reconnect, heartbeat monitor, bandwidth stats) and IoTTcpSession (IoT-friendly connect guard). |
| **UnreliableClient** | Lightweight UDP client for low-latency, best-effort messaging.                                       |
| **Extensions**       | Fluent control/directive builders, handshake (X25519), time sync, throttle handling, subscriptions.  |
| **Configuration**    | `TransportOptions` and related settings.                                                             |
| **L10N**             | Optional localization (e.g. `Localizer`, `MultiLocalizer`, PoFile).                                  |

---

## Documentation

|                                                    Document | Description                                                |
|-------------------------------------------------------------|------------------------------------------------------------|
| [TcpSession](./TcpSession.md)                       | Connect, send/receive, events, reconnect, heartbeat, framing, and transport options.       |
| [TcpSession Extensions](./TcpSession-Extensions.md) | Subscription helpers, control directives, handshake, throttling, directives, and request/response helpers. |

---

## Quick Start

```csharp
var client = new TcpSession();
client.OnConnected += (s, _) => { /* ... */ };
client.OnMessageReceived += (s, buf) => { /* handle packet; dispose buf if ownership taken */ };

await client.ConnectAsync("host.example.com", 12345);
await client.SendAsync(myPacket);
// Use extensions for handshake, ping, directives, etc.
await client.DisconnectAsync();
client.Dispose();
```

---

## See Also

- [Nalix.Network Architecture](../Nalix.Network/Architecture.md) — Server-side stack.
- [DOCUMENTATION.md](../../DOCUMENTATION.md) — Full documentation index.
