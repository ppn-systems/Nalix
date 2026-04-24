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

## Properties and Validation

| Property | Meaning | Default | Valid range |
|---|---|---:|---|
| `BucketCount` | Number of timing wheel buckets. Higher values reduce slot collisions at the cost of more buckets. | `512` | `1..int.MaxValue` |
| `TickDuration` | Tick interval in milliseconds. Lower values improve precision but increase check frequency. | `1000` | `1..int.MaxValue` |
| `IdleTimeoutMs` | TCP connection idle timeout threshold in milliseconds before auto-close. | `60000` | `1..int.MaxValue` |

## Validation Notes

`Validate()` uses `System.ComponentModel.DataAnnotations.Validator.ValidateObject(...)` and rejects values below `1` for every property.

## Related APIs

- [Timing Wheel](../time/timing-wheel.md)
- [TCP Listener](../tcp-listener.md)
