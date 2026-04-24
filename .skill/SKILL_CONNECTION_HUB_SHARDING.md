# 🌐 Nalix AI Skill — Connection Hub & Sharding

This skill covers the `ConnectionHub`, the central management point for all active server connections. It is optimized for high concurrency using sharding and O(1) operations.

---

## 🏗️ Sharding Architecture

To avoid lock contention in high-concurrency scenarios, the `ConnectionHub` divides connections into multiple **shards** (partitions).

- **`ConcurrentDictionary<UInt56, IConnection>[]`**: The internal storage is an array of dictionaries.
- **Shard Indexing:** A shard is selected based on the `ID.GetHashCode()`.
    - If `ShardCount` is power-of-two: `(hash & (ShardCount - 1))`.
    - Otherwise: `(hash % ShardCount)`.
- **Thread Safety:** Operations on one shard do not block other shards.

---

## 📜 Connection Lifecycle

### 1. Registration (`RegisterConnection`)
- **Capacity Check:** Ensures the server does not exceed `MaxConnections`.
- **Eviction Policy:** If the hub is full, the `DropPolicy` determines what happens:
    - `RejectNew`: New connection is dropped.
    - `DropOldest`: The oldest anonymous connection is evicted to make room.
- **Event Binding:** The hub listens to the connection's `OnCloseEvent` to automatically unregister it.

### 2. Lookup (`GetConnection`)
- **O(1) Access:** Lookups use the connection ID to jump directly to the correct shard.
- **Support:** Supports lookup by `ISnowflake`, `UInt56`, or `ReadOnlySpan<byte>`.

---

## ✉️ Broadcasting & Multicasting

### `BroadcastAsync<T>`
Sends a message to **all** active connections.
- **Optimization:** Captures a point-in-time snapshot of all shards and uses `Parallel.ForEach` or batched tasks for delivery.
- **Batching:** Controlled by `BroadcastBatchSize` to avoid overwhelming the network stack.

### `BroadcastWhereAsync<T>`
Sends a message only to connections matching a specific predicate (e.g., users in a specific room).

---

## 🛡️ Best Practices

- **Anonymous vs. Authenticated:** The hub tracks anonymous connections in a separate queue (`_anonymousQueue`) for prioritized eviction. Mark connections as authenticated as soon as possible.
- **Minimal Shard Locking:** Avoid performing heavy work while holding references to connections inside a shard iteration.
- **Graceful Shutdown:** Use `CloseAllConnections()` to notify and disconnect everyone cleanly during server maintenance.

---

## 🛡️ Common Pitfalls

- **Memory Leaks:** Forgetting to unregister a connection will keep it alive in the shard dictionary forever. Ensure `Dispose()` or `Disconnect()` is called.
- **Broadcast Amplification:** Broadcasting large packets to thousands of users simultaneously can cause spikes in CPU and Bandwidth. Use sharding and batching carefully.
- **Deadlocks:** Avoid calling `ConnectionHub` methods from within a connection event handler if it might trigger a recursive lock (though shards minimize this risk).
