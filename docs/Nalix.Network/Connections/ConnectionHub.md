# ConnectionHub — Sharded connection registry and bulk-ops manager

`ConnectionHub` is the central in-memory registry for live `IConnection` instances in Nalix.Network. It shards connections across multiple dictionaries, keeps username mappings, supports bulk broadcast and forced disconnect flows, and exposes runtime diagnostics.

## Mapped sources

- `src/Nalix.Network/Connections/Connection.Hub.cs`
- `src/Nalix.Network/Connections/Connection.Hub.Statistics.cs`
- `src/Nalix.Network/Connections/Connection.Hub.EventArgs.cs`

## Core design

- Connections are distributed across `_shards` using the connection ID hash.
- Usernames are tracked in two extra maps:
  - `ID -> username`
  - `username -> ID`
- Anonymous connections are also pushed into `_anonymousQueue` so `DROP_OLDEST` can evict in FIFO order without scanning every connection first.
- `Statistics` returns a structured snapshot with count, drop policy, shard count, anonymous queue depth, evicted count, and rejected count.

## Registration and unregister

`RegisterConnection(connection)`:

- rejects if the hub is disposed
- enforces `MaxConnections`
- subscribes to `connection.OnCloseEvent`
- inserts into the shard dictionary
- increments `_count`
- enqueues the connection ID into the anonymous FIFO

`UnregisterConnection(connection)`:

- removes the connection from the correct shard
- removes any username association from both maps
- unsubscribes from `OnCloseEvent`
- decrements `_count`
- raises `ConnectionUnregistered`

The optional `UnregisterDrainMillis` delay is currently a fire-and-forget `Task.Delay(...)`, so it does not block the unregister path.

## Username rules

`AssociateUsername(connection, username)`:

- ignores null / whitespace usernames
- optionally trims based on `TrimUsernames`
- truncates to `MaxUsernameLength`
- only accepts `^[a-zA-Z0-9_]+$`
- rebinds reverse mappings if the username changes

## Capacity limit behavior

When `MaxConnections` is reached:

- `DROP_NEWEST`: the new connection is disconnected and `_rejectedConnections` increments
- `DROP_OLDEST`: the hub dequeues anonymous IDs until it finds a live anonymous connection to evict

In both cases the hub raises `CapacityLimitReached` with a `ConnectionHubEventArgs` snapshot.

## Broadcast paths

- `BroadcastAsync<T>` sends to every connection.
- `BroadcastWhereAsync<T>` sends to filtered connections only.
- `BroadcastBatchSize > 0` enables batched `Task.WhenAll(...)` fan-out.
- Without batching, the hub partitions the connection list and processes partitions in parallel.

## Force close and shutdown

- `ForceClose(INetworkEndpoint)` disconnects every connection whose `NetworkEndpoint.Address` matches the target.
- `CloseAllConnections(reason)` disconnects all current connections in parallel, then clears shards, username maps, and the anonymous queue.
- `Dispose()` marks the hub disposed and calls `CloseAllConnections("disposed")`.

## Diagnostics

`GenerateReport()` prints:

- total, anonymous, and authenticated connection counts
- evicted and rejected counts
- shard count and anonymous queue depth
- configured max connection count and drop policy
- bytes sent and uptime aggregates
- per-level status summary
- per-algorithm summary
- the first 15 active connections with usernames

## See also

- [ConnectionHubOptions](../Configuration/ConnectionHubOptions.md)
- [Connection](./Connection.md)
