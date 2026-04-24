# Pooling Options

`PoolingOptions` configures network-layer object pool capacities and startup preallocation behavior.

## Audit Summary

- The previous version listed core pools but did not fully match the standard audit section pattern.
- Guidance was present but not explicitly tied to missing-content rationale.

## Missing Content Identified

- Explicit statement of consistency goals across options pages.
- Uniform audit framing comparable to other API references.

## Improvement Rationale

Consistent page shape helps operators quickly compare memory/throughput tuning knobs across subsystems.

## Source Mapping

- `src/Nalix.Network/Options/PoolingOptions.cs`

## Pool Groups (Current Defaults)

| Pool group | Capacity default | Preallocate default | Runtime owner |
|---|---:|---:|---|
| Accept context | `4096` | `20` | One context per in-flight TCP accept operation. |
| Socket async event args | `4096` | `32` | Shared by accept and receive paths. |
| Receive context | `4096` | `32` | One context per active TCP connection. |
| Timing wheel timeout task | `8192` | `64` | One timeout task per active timing-wheel registration. |
| Connect event context | `4096` | `32` | Queued connection callback dispatch wrappers. |

## Validation Notes

- Each capacity is validated in the range `1..1_000_000`.
- Each preallocate value is validated in the range `0..1_000_000`.
- Every `Preallocate` value must be `<=` its corresponding `Capacity`.

## Best Practices

- Size capacities for peak concurrency with headroom.
- Size preallocate values for steady-state warm usage.

## Related APIs

- [Timing Wheel](../time/timing-wheel.md)
- [TCP Listener](../tcp-listener.md)
