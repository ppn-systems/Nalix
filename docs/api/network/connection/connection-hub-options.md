# ConnectionHubOptions

`ConnectionHubOptions` configures capacity planning, username policy, sharding, broadcast batching, disconnect parallelism, and latency logging for `ConnectionHub`.

## Source mapping

- `src/Nalix.Network/Options/ConnectionHubOptions.cs`

## Important properties

| Property | Meaning | Default |
|---|---|---:|
| `InitialConnectionCapacity` | Initial sizing hint for connection storage. | `1024` |
| `MaxConnections` | Max live connections. `-1` means unlimited. | `-1` |
| `DropPolicy` | Action when max connections is reached. | `DROP_NEWEST` |
| `ParallelDisconnectDegree` | Max parallelism for bulk disconnect. | `-1` |
| `BroadcastBatchSize` | Batch size for broadcast fan-out. | `0` |
| `ShardCount` | Number of internal connection shards. | `CPU count` |
| `UnregisterDrainMillis` | Delay budget before unregistering on close. | `0` |
| `IsEnableLatency` | Enables latency timing logs. | `true` |

## Client guidance

For most systems, tune only:

- `MaxConnections`
- `DropPolicy`
- `BroadcastBatchSize`
- `ShardCount`

Change username-related options only if your identity format differs from the built-in assumptions.

## Example

```csharp
var options = new ConnectionHubOptions
{
    MaxConnections = 50_000,
    DropPolicy = ConnectionDropPolicy.DROP_NEWEST,
    BroadcastBatchSize = 256,
    ShardCount = Environment.ProcessorCount
};
```

## Related APIs

- [Connection Hub](./connection-hub.md)
