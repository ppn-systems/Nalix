# Nalix.SDK

## Role

Client-side development kit. Provides managed TCP/UDP session clients, request-response patterns, typed message subscriptions, time synchronization, and transport dispatching for applications connecting to Nalix servers.

**Dependencies:** `Nalix.Codec` (which transitively includes Abstractions + Environment)

## Directory Structure

```
Nalix.SDK/
├── Extensions/          # SDK extension methods (handshake, session resume, cipher update)
├── Options/             # SDK configuration options
├── Transport/           # Session clients
│   ├── TcpSession.cs               # Managed TCP client session
│   ├── UdpSession.cs               # Managed UDP client session
│   ├── TransportSession.cs         # Abstract base for transport sessions
│   ├── Extensions/                 # Transport-specific extension methods
│   └── Internal/                   # Internal transport helpers
├── IThreadDispatcher.cs            # Dispatcher abstraction (e.g., Unity main thread)
├── InlineDispatcher.cs             # Inline (same-thread) dispatcher
└── TimeSyncCalculator.cs           # NTP-style time synchronization calculator
```

## Key Components

### Transport Sessions

| Type | Purpose |
| :--- | :--- |
| `TransportSession` | Abstract base — shared lifecycle, connect/disconnect, receive loop. |
| `TcpSession` | TCP client with auto-reconnect, handshake, and encryption support. |
| `UdpSession` | UDP client with sequence tracking, HMAC, and fragment assembly. |

Sessions integrate with the full Codec pipeline (serialize → compress → encrypt).

### Request-Response Pattern

`RequestAsync<TResponse>()` API with:
- Correlated packet matching (via Snowflake correlation IDs).
- Configurable timeouts.
- Automatic deserialization of response type.

### Typed Message Subscriptions

`On<T>()` method for clean event-driven packet handling:
```csharp
session.On<ChatMessage>(msg => HandleChat(msg));
```

### Thread Dispatching

- `IThreadDispatcher` — Abstraction for dispatching callbacks to a specific thread (e.g., Unity main thread).
- `InlineDispatcher` — Executes callbacks on the receiving thread directly.

### Time Synchronization

`TimeSyncCalculator` — NTP-style round-trip time calculation for clock synchronization between client and server.

## AOT Compatibility

- `IsAotCompatible=true` with `TrimMode=partial`.
- `IlcOptimizationPreference=Speed`.
- Uses source-generated serialization — no reflection on hot paths.

## Anti-Patterns

- Do NOT bypass `TransportSession` lifecycle — use `ConnectAsync`/`DisconnectAsync`.
- Do NOT create raw sockets — use `TcpSession`/`UdpSession`.
- Do NOT forget to dispose sessions — they hold socket and buffer resources.
