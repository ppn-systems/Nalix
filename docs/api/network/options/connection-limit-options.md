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
| `MaxConnectionsPerIpAddress` | `int` | `10` | Max concurrent connections from a single IP. |
| `MaxConnectionsPerWindow` | `int` | `10` | Max new connection attempts from one IP in the rate window. |
| `BanDuration` | `TimeSpan` | `00:05:00` | Duration of IP ban after exceeding limits. |
| `ConnectionRateWindow` | `TimeSpan` | `00:00:05` | Sliding window used to count connection attempts. |
| `DDoSLogSuppressWindow` | `TimeSpan` | `00:00:20` | Suppresses repeated DDoS log entries for the same IP. |
| `CleanupInterval` | `TimeSpan` | `00:01:00` | Frequency of expired IP tracking cleanup. |
| `InactivityThreshold` | `TimeSpan` | `00:05:00` | Idle time before a connection is considered inactive. |
| `MaxUdpDatagramSize` | `int` | `1400` | Max inbound UDP datagram bytes. |
| `MaxErrorThreshold` | `int` | `50` | Max cumulative errors before forced disconnect. |
| `UdpReplayWindowSize` | `int` | `1024` | UDP anti-replay sliding window size in bits. |
| `MaxPacketPerSecond` | `int` | `128` | Max packets allowed per second per connection. |

## Validation

`Validate()` delegates to data annotations on the source type:

- `MaxConnectionsPerIpAddress`: `1..10_000`
- `MaxConnectionsPerWindow`: `1..10_000_000`
- `BanDuration`: `00:00:01..1.00:00:00`
- `ConnectionRateWindow`: `00:00:01..00:10:00`
- `DDoSLogSuppressWindow`: `00:00:01..01:00:00`
- `CleanupInterval`: `00:00:01..01:00:00`
- `InactivityThreshold`: `00:00:01..1.00:00:00`
- `MaxUdpDatagramSize`: `64..65507`
- `MaxErrorThreshold`: at least `1`
- `UdpReplayWindowSize`: `64..65536`
- `MaxPacketPerSecond`: `1..10_000_000`

## Why These Options Exist

They balance abuse resistance and legitimate NAT/shared-IP traffic behavior in admission control.

## Best Practices

- Tune `MaxConnectionsPerWindow`, `ConnectionRateWindow`, and `BanDuration` as a set.
- Monitor reject/ban metrics before tightening values.
- Keep cleanup/inactivity aligned with expected connection churn.
- Keep `MaxUdpDatagramSize` conservative enough to avoid fragmentation on the target network path.

## Related APIs

- [Connection Limiter](../connection/connection-limiter.md)
- [Network Options](./options.md)
