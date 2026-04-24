# Connection Hub Options

`ConnectionHubOptions` provides global configuration for the `IConnectionHub`, controlling the total capacity of the server, broadcast batching, and dictionary sharding for high-concurrency scenarios.

## Source Mapping

- `src/Nalix.Network/Options/ConnectionHubOptions.cs`

## Why This Type Exists

The `ConnectionHub` is the central repository for all active connections. As a server scales to tens of thousands of connections, global management requires fine-tuning to prevent lock contention and memory spikes.

## Configuration Table

| Option | Default | Validation | Description |
|---|---:|---|---|
| `MaxConnections` | `-1` | `-1` or positive; `0` rejected by `Validate()` | Total concurrent connections allowed server-wide. |
| `DropPolicy` | `DropNewest` | `DropPolicy` enum value | Rejection policy when the connection limit is reached. |
| `ParallelDisconnectDegree` | `-1` | `-1` or positive; `0` rejected by `Validate()` | Parallelism for bulk disconnect operations. |
| `BroadcastBatchSize` | `0` | `>= 0` | Connections processed per broadcast batch; `0` means no batching. |
| `ShardCount` | `max(1, Environment.ProcessorCount)` | `>= 1` | Number of internal connection dictionary shards. |
| `IsEnableLatency` | `true` | Boolean | Enables latency measurement for diagnostics and performance monitoring. |

## Internal Responsibilities (Source-Verified)

### 1. Dictionary Sharding

The hub shards connections across `ShardCount` buckets based on the `ConnectionId` hash.
This reduces contention versus a single global connection dictionary.

### 2. Broadcast Batching

Broadcast operations use `BroadcastBatchSize` to control how many connections are processed per batch.
The source default is `0`, which disables batching unless the operator opts in.

### 3. Drop Policies

When `MaxConnections` is reached, the hub uses `DropPolicy` to decide admission behavior:

- **DropNewest**: reject the incoming connection immediately.
- **DropOldest**: disconnect an existing connection to make room for the new one.

## Related Information Paths

- [Connection Hub](../connection/connection-hub.md)
- [Connection Limiter](../connection/connection-limiter.md)
- [Network Socket Options](./network-socket-options.md)
