# Time Synchronizer

`TimeSynchronizer` is a periodic tick service from `Nalix.Network.Pipeline` used by network/runtime integrations that require synchronized timing callbacks.

## Audit Summary

- Existing page was strong but needed explicit package-boundary note and tighter member mapping.

## Missing Content Identified

- Clear statement that this API belongs to `Nalix.Network.Pipeline` (not `Nalix.Network` core assembly).
- Explicit public members and lifecycle controls.

## Improvement Rationale

Boundary clarity avoids package confusion during adoption.

## Source Mapping

- `src/Nalix.Network.Pipeline/Timekeeping/TimeSynchronizer.cs`

## Public Surface

- `DefaultPeriod`
- `TimeSynchronized` event
- `IsRunning`
- `IsTimeSyncEnabled`
- `Period`
- `FireAndForget`
- `Activate(...)`
- `Deactivate(...)`
- `Restart()`
- `Dispose()`

## Why This Type Exists

It provides a managed periodic loop with explicit enable/disable control and optional fire-and-forget handler dispatch, while preserving low overhead for timing-sensitive flows.

## Best Practices

- Keep `TimeSynchronized` handlers lightweight.
- Use `FireAndForget` when handlers may block.
- Prefer explicit activation/deactivation during application lifecycle transitions.

## Related APIs

- [Timing Wheel](./timing-wheel.md)
- [Network Options](./options/options.md)
