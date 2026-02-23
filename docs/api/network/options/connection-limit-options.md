# ConnectionLimitOptions

`ConnectionLimitOptions` controls how `ConnectionGuard` enforces per-endpoint connection caps, burst detection, temporary bans, and cleanup of old limiter state.

## Source mapping

- `src/Nalix.Network/Options/ConnectionLimitOptions.cs`

## Properties

| Property | Meaning | Default |
|---|---|---:|
| `MaxConnectionsPerIpAddress` | Max concurrent connections from one IP. | `10` |
| `MaxConnectionsPerWindow` | Max connection attempts inside the rate window. | `10` |
| `BanDuration` | How long an offender stays banned. | `5 min` |
| `ConnectionRateWindow` | Sliding window for burst detection. | `5 sec` |
| `DDoSLogSuppressWindow` | Log suppression window per endpoint. | `20 sec` |
| `CleanupInterval` | How often limiter cleanup runs. | `1 min` |
| `InactivityThreshold` | Idle age before an entry is removable. | `5 min` |

## Practical tuning

Tune these together:

- `MaxConnectionsPerIpAddress`
- `MaxConnectionsPerWindow`
- `ConnectionRateWindow`
- `BanDuration`

If the numbers are too aggressive, real users behind NAT can get punished. If they are too loose, connection floods stay expensive longer.

`DDoSLogSuppressWindow` now also matters operationally because the limiter coalesces repeated reject and ban logs per endpoint instead of emitting one line per failure.

## Example

```csharp
var options = new ConnectionLimitOptions
{
    MaxConnectionsPerIpAddress = 5,
    MaxConnectionsPerWindow = 20,
    ConnectionRateWindow = TimeSpan.FromSeconds(10),
    BanDuration = TimeSpan.FromMinutes(2)
};
```

## Related APIs

- [Connection Limiter](../connection/connection-limiter.md)
- [Tcp Listener](../tcp-listener.md)
