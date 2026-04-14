# Connection Hub

`ConnectionHub` is the in-memory live-connection registry used by `Nalix.Network` for lookup, broadcast, disconnect orchestration, and session-store integration.

## Audit Summary

- Existing page was comprehensive but had some stale source mapping references and extra guidance outside strict API scope.
- Needed tighter coupling to currently exposed public members.

## Missing Content Identified

- Precise method/member list from current `Connection.Hub.cs`.
- Clear note that admission failures can surface as exceptions during registration.

## Improvement Rationale

This keeps operator and contributor expectations aligned with runtime behavior.

## Source Mapping

- `src/Nalix.Network/Connections/Connection.Hub.cs`
- `src/Nalix.Network/Options/ConnectionHubOptions.cs`

## Why This Type Exists

A single authoritative connection registry is required for server-wide operations: connection lookup, fan-out send, force-close, and session-aware management.

## Public Surface

- properties: `Count`, `SessionStore`, `Statistics`
- events: `ConnectionUnregistered`, `CapacityLimitReached`
- registration: `RegisterConnection(...)`, `UnregisterConnection(...)`
- lookup: `GetConnection(ISnowflake)`, `GetConnection(UInt56)`, `GetConnection(ReadOnlySpan<byte>)`
- enumeration: `ListConnections()`
- fan-out: `BroadcastAsync<T>(...)`, `BroadcastWhereAsync<T>(...)`
- control: `ForceClose(INetworkEndpoint)`, `CloseAllConnections(...)`
- diagnostics: `GenerateReport()`, `GetReportData()`
- lifecycle: `Dispose()`

## Operational Notes

- Connection keys are based on compact ID representation (`UInt56`) for lookup paths.
- Capacity behavior follows configured drop policy and raises capacity event callbacks.
- Session operations integrate through `SessionStore`.

## Best Practices

- Use hub as the authoritative live-session registry.
- Avoid holding long-lived stale connection references outside the hub lifecycle.
- Monitor capacity events and hub statistics in production.

## Related APIs

- [Connection](./connection.md)
- [Connection Hub Options](./connection-hub-options.md)
- [Session Store](../session-store.md)
