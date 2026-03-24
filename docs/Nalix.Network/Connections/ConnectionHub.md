# ConnectionHub — High-Performance, Sharded Connection Manager for .NET Servers

The `ConnectionHub` class provides the central connection tracking and management infrastructure for scalable, modern .NET backends (e.g., multiplayer games, API relays, gateways).
It combines high-throughput sharding with robust concurrency, bulk ops, username/ID mapping, and stats.

---

## Key Features

- **True sharding:**  
  - All connections are partitioned by ID across multiple dictionaries (per-CPU by default) for fast, lock-free access and O(1) concurrency scaling.
- **Username/user ID bi-directional map:**  
  - Efficient and safe handling of username associations, rebinding, and reverse lookups.
  - Applies regex/trim/length policies (configurable via ConnectionHubOptions) when associating usernames.
- **Anonymous/evictable queue:**  
  - Automatic O(1)-amortized eviction of "anonymous" (non-authenticated) connections under connection pressure.
- **Optimized broadcast:**  
  - Mass-message all, or subset, of connections with memory-efficient bulk sends or predicate-based targeting.
- **Eviction/drop policies:**  
  - Configurable: `DROP_NEWEST` (reject new when at cap) or `DROP_OLDEST` (autoevict oldest anonymous).
- **Connection info/monitoring:**  
  - Maintains per-connection stats (bytes sent, uptime, status, algorithm), and exposes live diagnostics/reporting.
- **Thread-safe, minimal allocations:**  
  - Carefully uses fast concurrent collections; can scale to tens of thousands of connections.

---

## Typical Usage Example

```csharp
var hub = new ConnectionHub();

// Register new connection
if (hub.RegisterConnection(conn))
{
    // connection is now trackable via ID, username, or hub.ListConnections()
}

// Map a username (post-auth)
hub.AssociateUsername(conn, "player123");

// Get by ID or username
var conn1 = hub.GetConnection(someId);
var conn2 = hub.GetConnection("player123");

// Broadcast to all
await hub.BroadcastAsync(msg, async (c, m) => await c.SendAsync(m), cancellationToken);

// Force close all for an IP
hub.ForceClose(new NetworkEndpoint("192.0.2.111"));

// Report & stats
Console.WriteLine(hub.GenerateReport());
```

---

## Drop/Eviction Policy

- When `MaxConnections` is reached, one of:
  - Drop newest (refuse new connections)
  - Drop oldest (evict the oldest anonymous/non-authenticated connections)
- Automatic queue logic for anonymous connections tracks FIFO order for efficient eviction.

---

## Methods Reference

| Method                                              | Description                                                        |
|-----------------------------------------------------|--------------------------------------------------------------------|
| `RegisterConnection(connection)`                    | Add a connection, returns bool success                             |
| `UnregisterConnection(connection)`                  | Remove connection from hub                                         |
| `AssociateUsername(connection, username)`           | Link a username to conn, updates maps                              |
| `GetConnection(id)`                                 | Get by connection ID                                               |
| `GetConnection(username)`                           | Get by associated username                                         |
| `ListConnections()`                                 | Return all current connections (read-only collection)              |
| `BroadcastAsync(msg, sendFunc, ct)`                 | Send message to all connections (sharded, batched, parallel)       |
| `BroadcastWhereAsync(msg, sendFunc, predicate, ct)` | Sends to matching subset                                           |
| `ForceClose(networkEndpoint)`                       | Disconnect all connections for a given IP/network endpoint         |
| `CloseAllConnections(reason)`                       | Gracefully disconnect ALL connections                              |
| `GenerateReport()`                                  | Produces a summary report (stats & per-connection listing)         |
| `Dispose()`                                         | Cleans up all resources, forcibly disconnects all connections      |

## Events

- `ConnectionUnregistered` is raised after a connection is removed; subscribe to clean up per-connection caches/middleware when the hub drops a client.


---

## Diagnostics Example

Calling `hub.GenerateReport()` produces a full cluster and connection status summary:

```log
[2026-03-12 15:35:00] ConnectionHub Status:
Total Connections    : 1752
Anonymous Users      : 800
Authenticated Users  : 952
Evicted Connections  : 9
Rejected Connections : 3
Total Bytes Sent     : 1,234,567
Average Uptime       : 935s
Max Connection Time  : 4223s
Min Connection Time  : 1s

Connection Status Summary:
Status          | Count
------------------------
USER            |   950
ADMIN           |     2
GUEST           |   800

Algorithm Summary:
Algorithm         | Count
-------------------------
CHACHA20_POLY1305 |   950
NONE              |   800

Active Connections:
ID             | Username
-------------------------
15654513...    | player123
12341513...    | (anonymous)
...
```

---

## Best Practices

- Use a **large enough MaxConnections** for your workload, but not so large it causes memory pressure.
- Assign a **username as soon as auth is complete** for fastest lookups and robust eviction handling.
- For broadcast-heavy environments, tune `BroadcastBatchSize` as needed.
- Always call `Dispose()` on shutdown to avoid dangling socket resources.

- Subscribe to `ConnectionUnregistered` to release per-connection caches or middleware state when a connection leaves the hub.
---

## License

Licensed under the Apache License, Version 2.0.  
Copyright (c) 2025-2026 PPN Corporation.

---

## See Also

- [TcpListenerBase.md](../Listeners/TcpListenerBase.md)
- [PoolingOptions.md](../Configurations/PoolingOptions.md)
- [ConnectionHubOptions.md](../Configurations/ConnectionHubOptions.md)
