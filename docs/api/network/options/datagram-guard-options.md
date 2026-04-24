# Datagram Guard Options

`DatagramGuardOptions` configures bounded UDP source-window tracking for datagram guard logic.
It limits how many IPv4 and IPv6 source windows can be retained and controls stale-window cleanup.

## Source Mapping

- `src/Nalix.Network/Options/DatagramGuardOptions.cs`

## Properties and Validation

| Property | Type | Default | Validation | Purpose |
|---|---|---:|---|---|
| `IPv4Windows` | `int` | `65536` | `1..10_000_000` | Maximum IPv4 source windows tracked at once. |
| `IPv6Windows` | `int` | `16384` | `1..10_000_000` | Maximum IPv6 source windows tracked at once. |
| `IPv4Capacity` | `int` | `1024` | `1..10_000_000` | Initial IPv4 source window map capacity. |
| `IPv6Capacity` | `int` | `64` | `1..10_000_000` | Initial IPv6 source window map capacity. |
| `CleanupInterval` | `TimeSpan` | `00:01:00` | `00:00:01..01:00:00` | How often stale source windows are purged. |
| `IdleTimeout` | `TimeSpan` | `00:00:10` | `00:00:01..01:00:00` | How long an inactive source window is retained before eviction. |

## Runtime Role

The datagram guard is separate from TCP connection admission control.
It bounds UDP source accounting so abusive or spoofed traffic cannot grow the tracking maps without limits.

## Best Practices

- Size `IPv4Windows` and `IPv6Windows` for expected active UDP source cardinality.
- Keep initial capacities lower than the maximum windows unless steady-state load justifies eager allocation.
- Tune `CleanupInterval` and `IdleTimeout` together so stale spoofed sources are reclaimed quickly without excessive cleanup churn.

## Related APIs

- [UDP Listener](../udp-listener.md)
- [Connection Limit Options](./connection-limit-options.md)
