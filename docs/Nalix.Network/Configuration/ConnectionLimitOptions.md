# ConnectionLimitOptions — Configuration for Per-IP Connection Limiting

The `ConnectionLimitOptions` class encapsulates all configuration settings for **per-IP rate/connection limiter** subsystems in a .NET server.  
These options help protect your server from client abuse, DoS, and resource starvation by governing how many simultaneous and rapid connections a single IP can make.

---

## Properties

| Property                       | Type         | Default    | Description                                                                           |
|--------------------------------|--------------|------------|---------------------------------------------------------------------------------------|
| `MaxConnectionsPerIpAddress`   | `int`        | `10`       | Max concurrent connections allowed from any single IP address (1–10,000 recommended). |
| `MaxConnectionsPerWindow`      | `int`        | `10`       | Max connection attempts from a single IP in the rate window below.                    |
| `BanDuration`                  | `TimeSpan`   | 5 min      | How long to ban an abusive IP if it exceeds the rate limit (1s–1 day).                |
| `ConnectionRateWindow`         | `TimeSpan`   | 5 sec      | Time window for calculating connection attempts ("bursts").                           |
| `DDoSLogSuppressWindow`        | `TimeSpan`   | 20 sec     | Time window for throttling repeated log events for DDoS/banned IPs.                   |
| `CleanupInterval`              | `TimeSpan`   | 1 min      | Interval for cleaning up unused/tracked IPs/resources (1s–1h).                        |
| `InactivityThreshold`          | `TimeSpan`   | 5 min      | How long before an inactive IP entry is garbage-collected (1s–1d).                    |

---

## Validation

- **All values are validated via data annotations and runtime checks.**
- Recommended to call `.Validate()` after loading via config to fail fast on deployment errors.

## Example

```csharp
ConnectionLimitOptions options = new ConnectionLimitOptions
{
    MaxConnectionsPerIpAddress = 32,
    MaxConnectionsPerWindow = 20,
    BanDuration = TimeSpan.FromMinutes(10),
    ConnectionRateWindow = TimeSpan.FromSeconds(10),
    CleanupInterval = TimeSpan.FromMinutes(1),
    InactivityThreshold = TimeSpan.FromMinutes(7)
};
options.Validate();
```

Pass this into your `ConnectionLimiter` on initialization.

---

## Best Practices

- Start with low values for new/exposed public servers; relax for internal or trusted environments.
- If running behind **NAT/load-balancer/reverse proxy**, consider real world connection multiplexing when setting limits.
- Use longer `BanDuration` for dealing with aggressive/automated clients.
- **Tune `CleanupInterval`** and `InactivityThreshold` to balance memory use and ban/policy enforcement persistency.

---

## Troubleshooting

- If legitimate users are blocked, raise thresholds or fine-tune window durations.
- If "zombie" or unused IP entries build up, shorten `InactivityThreshold` and lower `CleanupInterval`.

---

## License

Licensed under the Apache License, Version 2.0.  
Copyright (c) 2025 PPN Corporation.

---

## See Also

- [PoolingOptions.md](./PoolingOptions.md)
- [ConnectionLimiter.md](../Throttling/ConnectionLimiter.md)
