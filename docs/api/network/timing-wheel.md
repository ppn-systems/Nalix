# Timing Wheel

`TimingWheel` is the internal idle-timeout scheduler used by network listeners to close inactive connections.

## Audit Summary

- Existing page was conceptually strong but needed stronger emphasis that this type is internal (`Nalix.Network.Internal.Time`).
- Source mapping remains correct.

## Missing Content Identified

- Clear lifecycle ownership notes (activate/deactivate/register/unregister).
- Clarified relation to `TimingWheelOptions` and network pooling options.

## Improvement Rationale

This helps contributors reason about idle cleanup behavior and avoid misuse assumptions.

## Source Mapping

- `src/Nalix.Network/Internal/Time/TimingWheel.cs`
- `src/Nalix.Network/Options/TimingWheelOptions.cs`
- `src/Nalix.Network/Options/PoolingOptions.cs`

## Why This Type Exists

`TimingWheel` provides efficient idle-timeout scheduling without scanning all connections every tick.

## Core APIs

- `Activate(CancellationToken cancellationToken = default)`
- `Deactivate(CancellationToken cancellationToken = default)`
- `Register(IConnection connection)`
- `Unregister(IConnection connection)`
- `Dispose()`

## Operational Notes

- Uses hashed wheel buckets plus rounds for timeout scheduling.
- Registration state is tracked per connection with versioning to discard stale tasks safely.
- Timeout task objects are pool-managed and preallocated based on network `PoolingOptions`.

## Related APIs

- [Timing Wheel Options](./options/timing-wheel-options.md)
- [Pooling Options](./options/pooling-options.md)
- [TCP Listener](./tcp-listener.md)
