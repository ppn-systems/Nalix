# Connection Hub

`ConnectionHub` is the central authoritative registry for all active client connections. It provides high-performance thread-safe storage, O(1) lookups, and orchestration for server-wide operations like broadcasting and bulk disconnects.

## Source Mapping

- `src/Nalix.Abstractions/Networking/IConnection.Hub.cs`
- `src/Nalix.Network/Connections/Connection.Hub.cs`
- `src/Nalix.Network/Sessions/SessionService.cs`

## Why This Type Exists

As a stateful server scales, managing the lifecycle of tens of thousands of concurrent connections becomes a performance bottleneck. `ConnectionHub` solves this by:

- **Shard-Aware Storage**: Fragmenting the connection pool into multiple internal dictionaries to eliminate lock contention during high-concurrency registration and removal.
- **Atomic Admission Control**: Enforcing global connection limits with configurable drop policies (Drop Oldest vs. Drop Newest).
- **Session Integration**: Acting as the gateway to the `ISessionService` for resuming cryptographic states.

## Connection Registry Architecture

The following diagram illustrates how the Hub manages its internal shards and handles registration requests.

```mermaid
flowchart TD
    Req[RegisterConnection Request] --> Capacity{Capacity Full?}
    
    Capacity -->|No| Sharding[Calculate Shard Index - id % ShardCount]
    Capacity -->|Yes| Policy{DropPolicy?}
    
    Policy -->|DropNewest| Reject[Reject with Exception]
    Policy -->|DropOldest| Evict[Evict Oldest Anonymous Conn]
    
    Evict --> Sharding
    Sharding --> Add[Add to ConcurrentDictionary]
    Add --> Event[Raise ConnectionUnregistered on Close]

    subgraph Shards[Internal Fragmented Storage]
        Shard0[Shard 0]
        Shard1[Shard 1]
        ShardN[Shard N]
    end

    Sharding -.-> Shards
```

## Internal Responsibilities (Source-Verified)

### 1. Dictionary Fragmentation (Sharding)

The hub splits connections across `ShardCount` internal dictionaries (standard is `ProcessorCount`).

- **Hash Spreading**: The `ulong` Connection ID is hashed to determine which shard owns it.
- **Concurrency**: This allows multiple CPU cores to register or unregister connections independently without waiting for a global lock on the entire hub.

### 2. Admission and Eviction

When `MaxConnections` is enabled:

- **DropNewest**: The default behavior. Rejects new handshakes when the server is full.
- **DropOldest**: If the hub is full, it identifies the oldest **Anonymous** (not yet authenticated) connection from an internal `ConcurrentQueue` and forcibly evicts it to make room for the new arrival.

### 3. Resilience & Session Persistence

To protect the server from memory exhaustion and ensure reliable state recovery:

- **Auto-Persist on Unregister**: When `_sessionOptions.AutoSaveOnUnregister` is enabled, `TryUnregisterCore(...)` starts a background `SaveSessionAsync(connection)` call on the configured `ISessionService`.
- **Policy lives in the session service**: The "only persist established, meaningful sessions" rule is enforced by `SessionService.SaveSessionAsync(IConnection)`, including the handshake-established check and `MinAttributesForPersistence` threshold.
- **Fire-and-forget storage**: Unregister stays low-latency because persistence failures are swallowed in the background helper instead of blocking removal.

### 4. Batched Broadcasting

Broadcasting to large numbers of clients is performed using `CaptureConnectionSnapshot()`, which rents an array from `ArrayPool<IConnection>` to avoid GC pressure.

- **Parallel Dispatch**: Broadcasts can be batched to interleave I/O operations and maintain responsive network processing for non-participating clients.

## Public APIs

- `Count`: The total number of live connections (uses `Volatile.Read` for accuracy).
- `SessionService`: Access to the underlying session persistence layer.
- `ConnectionUnregistered`: Event raised after a connection is successfully unregistered.
- `CapacityLimitReached`: Event raised when a limit is reached and a connection is rejected.
- `RegisterConnection(conn)`: Enrolls a new connection (Thread-safe).
- `UnregisterConnection(conn)`: Removes a connection from the hub.
- `GetConnection(id)`: O(1) retrieval by Snowflake ID.
- `ListConnections()`: Returns a read-only collection of all active connections.
- `BroadcastAsync<T>(msg, sendFunc)`: High-performance fan-out.
- `BroadcastWhereAsync<T>(msg, sendFunc, predicate)`: Broadcasts only to connections matching the predicate.
- `ListConnections(INetworkEndpoint)`: Returns active connections from a specific endpoint address.
- Bulk termination is handled by `IConnectionTerminator.CloseAll(...)` and `IConnectionTerminator.CloseByEndpoint(...)`.
- `Dispose()`: Releases all resources and closes all connections.

## Best Practices

!!! tip "Broadcast Filtering"
    Always use `BroadcastWhereAsync<T>` if you only need to send data to a subset of clients (e.g., players in the same game room). This prevents unnecessary packet serialization for clients that don't need the update.

!!! warning "Locking Caution"
    `ConnectionHub` is thread-safe, but its methods should not be called inside sensitive locks in your application code, as this could lead to deadlocks with internal Shard locks during concurrent unregistration.

## Related Information Paths

- [Connection](./connection.md)
- [Connection Hub Options](../../options/network/connection-hub-options.md)
- [Timing Wheel](../time/timing-wheel.md)
- [Session Store & Service](../session-store.md)
