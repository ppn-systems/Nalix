# Session Diagnostics

Nalix SDK does not currently expose a dedicated diagnostics snapshot API (for example, no `GetDiagnostics()` extension and no `TcpSessionDiagnostics` model in `src/Nalix.SDK`).
Operational diagnostics come from transport state and lifecycle events on `TransportSession`, `TcpSession`, and `UdpSession`.

## Audit Summary

- Previous page referenced non-existent APIs.
- Required rewrite to match the implemented SDK surface exactly.

## Missing Content Identified

- No explicit snapshot/metrics DTO in SDK transport APIs.
- No dedicated diagnostics extension file in `Nalix.SDK`.

## Improvement Rationale

Documenting real observability surfaces avoids integration code built on unavailable APIs.

## Source Mapping

- `src/Nalix.SDK/Transport/TransportSession.cs`
- `src/Nalix.SDK/Transport/TcpSession.cs`
- `src/Nalix.SDK/Transport/UdpSession.cs`
- `src/Nalix.SDK/Transport/Extensions/TcpSessionSubscriptions.cs`

## Available Diagnostics Signals

## 1. Session health and lifecycle

From `TransportSession` and concrete implementations:

- `IsConnected`
- `OnConnected`
- `OnDisconnected`
- `OnError`

These are the primary signals for connection state transitions and transport failures.

## 2. Message-flow observation

- `OnMessageReceived` (raw frame lease/event surface)
- `TcpSession.OnMessageAsync` (async callback for received frame payloads)

Use these to instrument receive volume, handler latency, and packet decode outcomes in your application layer.

## 3. Subscription-level error tracing

`TcpSessionSubscriptions` catches handler exceptions and writes diagnostics via `System.Diagnostics.Trace` (`TraceWarning` / `TraceError`), instead of propagating exceptions to the receive loop.

## Practical Pattern

```csharp
client.OnConnected += (_, _) => metrics.ConnectionOpened();
client.OnDisconnected += (_, ex) => metrics.ConnectionClosed(ex.Message);
client.OnError += (_, ex) => metrics.TransportError(ex);
client.OnMessageReceived += (_, lease) =>
{
    metrics.BytesIn(lease.Length);
    lease.Dispose();
};
```

## Best Practices

- Centralize event subscription and telemetry wiring when a session is created.
- Always handle `OnError` and `OnDisconnected` separately; they represent different runtime states.
- For UI/main-thread apps, marshal callbacks before touching thread-affine components.

## Related APIs

- [Transport Session](./transport-session.md)
- [TCP Session](./tcp-session.md)
- [UDP Session](./udp-session.md)
- [Session Subscriptions](./subscriptions.md)
- [Thread Dispatching](./thread-dispatching.md)
