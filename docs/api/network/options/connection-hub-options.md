# Connection Hub Options

`ConnectionHubOptions` provides global configuration for the `IConnectionHub`, controlling the total capacity of the server, broadcast batching, and dictionary sharding for high-concurrency scenarios.

## Source Mapping

- `src/Nalix.Network/Options/ConnectionHubOptions.cs`

## Why This Type Exists

The `ConnectionHub` is the central repository for all active connections. As a server scales to tens of thousands of connections, global management requires fine-tuning to prevent lock contention and memory spikes.

## Configuration Table

| Option | Description | Typical Value |
|---|---|---|
| `MaxConnections` | Total concurrent connections allowed server-wide. `-1` means unlimited. | -1 or 50,000+ |
| `DropPolicy` | How to handle new connections when `MaxConnections` is reached (`DropNewest` or `DropOldest`). | `DropNewest` |
| `ShardCount` | The number of internal dictionary shards. More shards reduce lock contention. | `ProcessorCount` |
| `BroadcastBatchSize` | Connections processed per broadcast batch. `0` disables batching. | 0 or 1000 |
| `ParallelDisconnectDegree` | Parallelism for bulk disconnect operations. `-1` uses ThreadPool default. | -1 |
| `IsEnableLatency` | Enables real-time latency diagnostics for every connection. | `true` |

## Internal Responsibilities (Source-Verified)

### 1. Dictionary Sharding
To achieve extreme throughput, the hub doesn't use a single dictionary. Instead, it shards connections across `ShardCount` buckets based on the `ConnectionID` hash. 
- This ensures that while one thread is modifying connections in Shard A, other threads can simultaneously work on Shard B without waiting for a global lock.

### 2. Broadcast Batching
Broadcasting a packet to 10,000+ connected clients can cause a massive burst of I/O. 
- Setting `BroadcastBatchSize` allows the hub to split the broadcast into chunks, allowing the ThreadPool to interleave other network operations between batches.

### 3. Drop Policies
When `MaxConnections` is reached, the hub uses the `DropPolicy` to decide the fate of incoming traffic:
- **DropNewest**: Rejects the incoming connection immediately. Efficient for maintaining stability.
- **DropOldest**: Disconnects the longest-running connection to make room for the new one. Useful for systems where only the most recent activity matters.

## Related Information Paths

- [Connection Hub](../connection/connection-hub.md)
- [Connection Limiter](../connection/connection-limiter.md)
- [Network Socket Options](./network-socket-options.md)
