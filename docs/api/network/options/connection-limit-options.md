# Connection Limit Options

`ConnectionLimitOptions` configures per-endpoint admission limits and cleanup behavior used by `ConnectionGuard`.

## Audit Summary

- The page content was accurate but more concise than the rest of the API set.
- It lacked explicit separation between identified gaps and rationale.

## Missing Content Identified

- Explicit rationale for why these knobs must be tuned together.
- Consistent structure with other API option pages.

## Improvement Rationale

A consistent documentation shape reduces scanning cost and makes operational playbooks easier to compare across options.

## Source Mapping

- `src/Nalix.Network/Options/ConnectionLimitOptions.cs`

## Core Properties (Current Defaults)

| Property | Type | Default | Purpose |
|---|---|---:|---|
| `MaxConnectionsPerIpAddress` | `int` | `8` | Max concurrent connections from a single IP. |
| `MaxConnectionsPerWindow` | `int` | `100` | Max new connections allowed in a rate window. |
| `BanDuration` | `TimeSpan` | `00:10:00` | Duration of IP ban after exceeding limits. |
| `MaxUdpDatagramSize` | `int` | `1400` | Max inbound UDP bytes (to avoid fragmentation). |
| `MaxErrorThreshold` | `int` | `50` | Max cumulative errors before forced disconnect. |
| `UdpReplayWindowSize` | `int` | `1024` | UDP anti-replay sliding window size (bits). |
| `MaxPacketPerSecond` | `int` | `128` | Max packets allowed per second per connection. |
| `CleanupInterval` | `TimeSpan` | `00:01:00` | Frequency of inactive connection pruning. |
| `InactivityThreshold` | `TimeSpan` | `00:05:00` | Max idle time before forced closure. |

## Why These Options Exist

They balance abuse resistance and legitimate NAT/shared-IP traffic behavior in admission control.

## Best Practices

- Tune window + ban settings as a set.
- Monitor reject/ban metrics before tightening values.
- Keep cleanup/inactivity aligned with expected connection churn.

## Related APIs

- [Connection Limiter](../connection/connection-limiter.md)
- [Network Options](./options.md)
