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

## Core Properties

- `MaxConnectionsPerIpAddress`
- `MaxConnectionsPerWindow`
- `BanDuration`
- `ConnectionRateWindow`
- `DDoSLogSuppressWindow`
- `CleanupInterval`
- `InactivityThreshold`

## Why These Options Exist

They balance abuse resistance and legitimate NAT/shared-IP traffic behavior in admission control.

## Best Practices

- Tune window + ban settings as a set.
- Monitor reject/ban metrics before tightening values.
- Keep cleanup/inactivity aligned with expected connection churn.

## Related APIs

- [Connection Limiter](../connection/connection-limiter.md)
- [Network Options](./options.md)
