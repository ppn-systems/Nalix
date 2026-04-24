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

## Properties and Validation

| Property | Default | Valid range | Runtime effect |
|---|---:|---|---|
| `MaxPerConnectionPendingPackets` | `16` | `1..1024` | Layer 1 cap for queued-but-not-yet-processed packets per connection. |
| `MaxPerConnectionOpenFragmentStreams` | `4` | `1..256` | Layer 1 cap for concurrently open fragmented streams per connection. |
| `MaxPendingNormalCallbacks` | `10000` | `100..1000000` | Layer 2 global cap for normal-priority callbacks pending in `AsyncCallback`. |
| `CallbackWarningThreshold` | `5000` | `0..1000000` | Logs a warning whenever pending callbacks reach multiples of this value; `0` disables warnings. |
| `MaxPendingPerIp` | `64` | `1..10000` | Layer 2 per-IP fairness cap for normal-priority callbacks. |
| `MaxPooledCallbackStates` | `1000` | `64..100000` | Maximum reusable `StateWrapper` objects retained by `AsyncCallback`. |
| `FairnessMapSize` | `4096` | `1024..65536` | Fixed-size array used for per-IP fairness tracking. |

## Layer Model

### Layer 1 (Per Connection)

`MaxPerConnectionPendingPackets` and `MaxPerConnectionOpenFragmentStreams` protect the receive path before packets are allowed to accumulate work for the dispatcher.

### Layer 2 (Callback Dispatcher)

`MaxPendingNormalCallbacks`, `CallbackWarningThreshold`, `MaxPendingPerIp`, `MaxPooledCallbackStates`, and `FairnessMapSize` tune `AsyncCallback` queue pressure and per-IP fairness. High-priority close/disconnect callbacks bypass the normal callback caps.

## Validation Notes

- Data annotations validate each individual range shown above.
- `CallbackWarningThreshold` must be less than `MaxPendingNormalCallbacks` when warnings are enabled.
- `MaxPendingPerIp` must not exceed `MaxPendingNormalCallbacks`.

## Related APIs

- [Connection Limiter](../connection/connection-limiter.md)
- [Network Options](./options.md)
