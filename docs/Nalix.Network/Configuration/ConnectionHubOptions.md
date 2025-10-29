# ConnectionHubOptions — Configuration for ConnectionHub Pooling, Limits, & Broadcast

The `ConnectionHubOptions` class configures capacity, username, concurrency, and broadcast behavior in your `ConnectionHub`, a central component for large-scale .NET backend connection management.

---

## Key Properties

| Property                    | Type    | Default         | Description                                                                   |
|-----------------------------|---------|-----------------|-------------------------------------------------------------------------------|
| `InitialConnectionCapacity` | int     | 1024            | Initial connection map capacity (dictionary size).                            |
| `InitialUsernameCapacity`   | int     | 1024            | Initial username map capacity.                                                |
| `MaxConnections`            | int     | -1              | Max number of concurrent connections allowed (-1: unlimited).                 |
| `DropPolicy`                | enum    | DROP_NEWEST     | What to do when max is reached (`DROP_NEWEST`, `DROP_OLDEST`).                |
| `MaxUsernameLength`         | int     | 64              | Maximum allowed username length (characters).                                 |
| `TrimUsernames`             | bool    | true            | Automatically trim whitespace from usernames on associate.                    |
| `ParallelDisconnectDegree`  | int     | -1              | Max number of parallel tasks for disconnects (-1: use ThreadPool default).    |
| `BroadcastBatchSize`        | int     | 0               | Batch size when broadcasting (0: disables batching).                          |
| `UnregisterDrainMillis`     | int     | 0               | Milliseconds to wait after OnCloseEvent before fully unregistering.           |
| `IsEnableLatency`           | bool    | true            | Enables latency/perf diagnostics logging in registration and broadcasts.      |

---

## Validation Rules

- All values are validated at runtime.
- `MaxConnections = 0` is forbidden (use `-1` for unlimited).
- `MaxUsernameLength` must be in [1, 1024].
- `InitialConnectionCapacity` and `InitialUsernameCapacity` must be ≥ 1.
- `ParallelDisconnectDegree = 0` is forbidden (use `-1` for default).
- Throws at startup on misconfiguration.

---

## Usage Example

```csharp
var options = new ConnectionHubOptions
{
    MaxConnections = 5000,
    DropPolicy = DropPolicy.DROP_OLDEST,
    BroadcastBatchSize = 100,
    InitialConnectionCapacity = 4096,
    // ...other tuning as required
};
options.Validate();
var hub = new ConnectionHub(); // Will auto-pick up these options via configuration system
```

---

## Notes

- **DropPolicy:**
  - `DROP_NEWEST`: New connection is rejected when at capacity.
  - `DROP_OLDEST`: Oldest anonymous is evicted to make room.
- **Performance:**  
  - Batch & parallel controls are critical for massive-scale broadcast or disconnect ops.
  - Enable `IsEnableLatency` for on-call or perf-tuning environments only.

---

## Troubleshooting

- If hit user/connection mapping limits, raise initial capacities.
- If registration latency logging is noisy, disable `IsEnableLatency`.
- In heavy broadcast environments, increase `BroadcastBatchSize` for throughput.

---

## License

Licensed under the Apache License, Version 2.0.  
Copyright (c) 2025 PPN Corporation.

---

## See Also

- [PoolingOptions.md](./PoolingOptions.md)
- [ConnectionHub.md](../Connections/ConnectionHub.md)
