# Network Callback Options

`NetworkCallbackOptions` controls callback queue pressure and per-connection/per-IP callback throttling behavior.

## Audit Summary

- The page described the two-layer model correctly but was shorter than the standard structure.
- It lacked explicit improvement rationale language.

## Missing Content Identified

- A clear explanation of why the layered throttle model matters operationally.
- Full consistency with section ordering used across API docs.

## Improvement Rationale

Standardized section ordering improves maintainability and makes future diffs easier to review.

## Source Mapping

- `src/Nalix.Network/Options/NetworkCallbackOptions.cs`

## Layer Model

### Layer 1 (Per Connection)

- `MaxPerConnectionPendingPackets`
- `MaxPerConnectionOpenFragmentStreams`

### Layer 2 (Callback Dispatcher)

- `MaxPendingNormalCallbacks`
- `CallbackWarningThreshold`
- `MaxPendingPerIp`
- `MaxPooledCallbackStates`

## Validation Notes

- `CallbackWarningThreshold` must be less than `MaxPendingNormalCallbacks` (when enabled).
- `MaxPendingPerIp` must not exceed `MaxPendingNormalCallbacks`.

## Related APIs

- [Connection Limiter](../connection/connection-limiter.md)
- [Network Options](./options.md)
