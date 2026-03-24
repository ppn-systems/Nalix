# Timing Wheel Options

`TimingWheelOptions` configures idle-timeout scheduling behavior for connection timeout cleanup.

## Audit Summary

- The page was technically correct but minimal compared with neighboring option pages.
- It lacked explicit missing-content and rationale sections.

## Missing Content Identified

- Consistent audit framing and rationale text.
- Uniform structure for fast scanning across all option pages.

## Improvement Rationale

A standardized structure supports quicker onboarding and lowers documentation review friction.

## Source Mapping

- `src/Nalix.Network/Options/TimingWheelOptions.cs`

## Properties

| Property | Meaning | Default |
|---|---|---:|
| `BucketCount` | Number of timing wheel buckets. | `512` |
| `TickDuration` | Tick interval (ms). | `1000` |
| `IdleTimeoutMs` | Idle timeout threshold (ms). | `60000` |

## Related APIs

- [Timing Wheel](../time/timing-wheel.md)
- [TCP Listener](../tcp-listener.md)
