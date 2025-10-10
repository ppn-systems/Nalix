# ConnectionHubOptions — ConnectionHub tuning surface

`ConnectionHubOptions` configures capacity planning, username policy, sharding, broadcast batching, disconnect parallelism, and latency logging for `ConnectionHub`.

## Mapped source

- `src/Nalix.Network/Configurations/ConnectionHubOptions.cs`

## Properties

| Property | Default | Meaning |
|---|---:|---|
| `InitialConnectionCapacity` | `1024` | Initial sizing hint for connection storage. |
| `InitialUsernameCapacity` | `1024` | Initial sizing hint for username maps. |
| `MaxConnections` | `-1` | Max live connections. `-1` means unlimited. |
| `DropPolicy` | `DROP_NEWEST` | What `ConnectionHub` does when `MaxConnections` is reached. |
| `MaxUsernameLength` | `64` | Username length cap after trimming. |
| `TrimUsernames` | `true` | Trim leading and trailing whitespace before storing usernames. |
| `ParallelDisconnectDegree` | `-1` | Max degree of parallelism for bulk disconnects. |
| `BroadcastBatchSize` | `0` | Batch size for broadcast fan-out. `0` disables batching. |
| `ShardCount` | `Environment.ProcessorCount` | Number of connection shards used internally. |
| `UnregisterDrainMillis` | `0` | Delay budget before unregistering on close. |
| `IsEnableLatency` | `true` | Enables latency timing logs for register / unregister / broadcast paths. |

## Validation rules

`Validate()` enforces:

- `InitialConnectionCapacity >= 1`
- `InitialUsernameCapacity >= 1`
- `MaxConnections` must be `-1` or positive, but not `0`
- `MaxUsernameLength` must be between `1` and `1024`
- `ParallelDisconnectDegree` must be `-1` or positive, but not `0`
- `BroadcastBatchSize >= 0`
- `ShardCount >= 1`
- `UnregisterDrainMillis >= 0`

## Operational notes

- `ShardCount` directly affects how many internal dictionaries `ConnectionHub` creates.
- `DropPolicy.DROP_OLDEST` only evicts anonymous connections; authenticated users are not candidates in that branch.
- `IsEnableLatency` is useful for diagnostics, but it increases log volume.

## See also

- [ConnectionHub](../Connections/ConnectionHub.md)
