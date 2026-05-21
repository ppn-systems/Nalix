# Connection Guard Options

`ConnectionGuardOptions` configures connection protection against abuse, including packet-per-second thresholds, progressive banning schedules, permanent blacklists, error count thresholds, and DDoS log suppression windows.

## Source Mapping

- `src/Nalix.Network/Options/ConnectionGuardOptions.cs`
- `src/Nalix.Network/RateLimiting/Connection.Guard.cs`
- `src/Nalix.Network/Connections/Connection.cs`
- `src/Nalix.Hosting/Bootstrap.cs`

## Defaults and Validation

| Property | Default | Validation | Runtime consumer |
| --- | ---: | --- | --- |
| `BanDuration` | `00:05:00` | `00:00:01..1.00:00:00` | Ban length after connection-attempt abuse. |
| `DDoSLogSuppressWindow` | `00:00:20` | `00:00:01..01:00:00` | Per-endpoint suppress window for reject, DDoS, and close logs. |
| `MaxErrorThreshold` | `50` | `1..int.MaxValue` | Per-connection error count threshold before disconnect. |
| `MaxPacketPerSecond` | `128` | `1..10_000_000` | Packet rate limiter budget per connection. |
| `BlacklistedIpsString` | `""` | IP address lists | List of permanently blocked IPs. |
| `EnableProgressiveBanning` | `true` | `bool` | Enables progressive escalation schedules for ban durations. |

`Validate()` uses manual range checks and throws `ArgumentOutOfRangeException` when constraints are violated.

## Hosting Initialization

`Bootstrap.Initialize()` loads `ConnectionGuardOptions` during server startup so the server configuration template includes every protection knob:

```csharp
_ = ConfigurationManager.Instance.Get<ConnectionGuardOptions>();
```

## Ban and Progressive Banning Behavior

When an endpoint triggers a rate limit violation in `ConnectionGuard`:

1. It calculates the ban duration using a progressive escalation schedule if `EnableProgressiveBanning` is true:
   - 1st ban: 1 minute
   - 2nd ban: 5 minutes
   - 3rd ban: 15 minutes
   - 4th ban: 1 hour
   - 5th ban: 6 hours
   - 6th ban: 24 hours
   - Subsequent bans: Cap at 24 hours
   If `EnableProgressiveBanning` is false, it uses the static `BanDuration`.

2. It sets `BannedUntilTicks` to the calculated duration.
3. It emits a throttled DDoS warning.
4. It rejects the connection attempt.
5. It schedules a `TaskManager` worker to invoke the registered endpoint-termination callback to sever all active connections from the banned IP.

The DDoS-detected logs are suppressed using the `DDoSLogSuppressWindow` configuration to prevent log amplification during high-volume connection floods.

## Connection Error Protection

`Connection.IncrementErrorCount()` compares the cumulative per-connection error count against `MaxErrorThreshold`. When the threshold is reached, the connection calls `Disconnect("Exceeded maximum error threshold.")` to prevent persistent noisy or malformed connections from consuming CPU resources.

## Related APIs

- [Connection Limiter](../../network/connection/connection-limiter.md)
- [Connection Quota Options](./connection-quota-options.md)
- [Trusted Proxy Options](./trusted-proxy-options.md)
- [Network Options](./options.md)
