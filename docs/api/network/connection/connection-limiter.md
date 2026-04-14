# Connection Limiter

`ConnectionGuard` is the per-endpoint admission limiter used by `TcpListenerBase` to protect connection acceptance under burst or abusive traffic.

## Audit Summary

- Existing page was directionally correct; needed stronger alignment to concrete `ConnectionGuard` API and cleanup/log-throttle behavior.

## Missing Content Identified

- Explicit mention of close-event feedback loop (`OnConnectionClosed`) and report APIs.
- Clarified boundary between admission guard and broader connection hub policy.

## Improvement Rationale

This improves operational tuning and avoids stale per-endpoint counters.

## Source Mapping

- `src/Nalix.Network/Connections/Connection.Guard.cs`
- `src/Nalix.Network/Options/ConnectionLimitOptions.cs`

## Why This Type Exists

`ConnectionGuard` enforces fast, per-endpoint admission checks before expensive protocol/dispatch work begins.

## Core Public Surface

- `TryAccept(IPEndPoint endPoint)`
- `OnConnectionClosed(object? sender, IConnectEventArgs args)`
- `GenerateReport()`
- `GetReportData()`
- `WithLogging(ILogger logger)`
- `Dispose()` / `DisposeAsync()`

## Operational Notes

- Admission decisions include per-endpoint concurrency cap, windowed attempt cap, and temporary bans.
- Repeated reject/DDOS/close logs are throttled per endpoint.
- Cleanup runs periodically to remove stale endpoint state.

## Best Practices

- Always wire `connection.OnCloseEvent += guard.OnConnectionClosed`.
- Validate `ConnectionLimitOptions` at startup.
- Use `GetReportData()` for metrics pipelines and `GenerateReport()` for operator diagnostics.

## Related APIs

- [TCP Listener](../tcp-listener.md)
- [Connection Limit Options](../options/connection-limit-options.md)
- [Connection Hub](./connection-hub.md)
