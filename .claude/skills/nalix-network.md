# Nalix.Network

## Role

High-performance networking transport runtime. Provides TCP/UDP listeners, connection/hub management, protocol lifecycle, session persistence, and rate limiting for real-time server applications.

**Dependencies:** `Nalix.Abstractions`, `Nalix.Framework`

## Directory Structure

```
Nalix.Network/
├── Connections/         # Connection implementation and hub
│   ├── Connection.cs               # Core connection (implements IConnection)
│   ├── Connection.Hub.cs           # ConnectionHub (shard-based connection management)
│   └── Connection.EventArgs.cs     # Connection event data
├── Internal/            # Internal networking helpers
├── Listeners/           # Transport listeners
│   ├── TcpListener/                # TCP accept loop and socket management
│   └── UdpListener/                # UDP receive loop with anti-replay
├── Options/             # Network configuration options
├── Protocols/           # Protocol lifecycle orchestration
├── RateLimiting/        # IP-based and connection-based rate limiters
├── Sessions/            # Session persistence
│   ├── SessionStoreBase.cs         # Abstract session store
│   └── InMemorySessionStore.cs     # In-memory session store with TTL
```

## Key Components

### Connection & ConnectionHub

- `Connection` implements `IConnection` — represents a single client socket.
- `Connection.Hub` (`ConnectionHub`) — **shard-based** concurrent dictionary for O(1) lookup/broadcast.
- Sharding prevents lock contention under thousands of concurrent connections.
- `BroadcastAsync` sends to all connections across shards.

### TCP Listener

- Non-blocking accept loop using `Socket.AcceptAsync`.
- Each accepted connection gets a `SocketConnection` with dedicated receive loop.
- Integrates with `FramePipeline` for transform (decompress → decrypt → deserialize).

### UDP Listener

- Single receive loop with `ReceiveFromAsync`.
- Anti-replay: sliding window + sequence ID validation.
- HMAC integrity verification and timestamp checks baked into the listener pipeline.

### Session Management

- `SessionStoreBase` — Abstract base with TTL-based expiration.
- `InMemorySessionStore` — `MemoryCache`-backed implementation.
- Session snapshots are created **immediately** after handshake (not on disconnect).
- Supports zero-RTT resume via `SessionResume` protocol frame.

### Rate Limiting

`ConnectionGuard` — IP-based connection rate limiting using a sharded entry map. Split into partial files:

| File | Content |
| :--- | :--- |
| `Connection.Guard.cs` | Core allow/deny logic, shard lookup, ban enforcement |
| `Connection.Guard.Types.cs` | `ConnectionAllowResult`, `ConnectionLimitInfo` (immutable snapshot), `ConnectionLimitEntry` (mutable state with `SpinLock`, sliding-window timestamps, ban tier tracking) |
| `Connection.Guard.Cleanup.cs` | TTL-based stale entry eviction |
| `Connection.Guard.Report.cs` | Diagnostics and metrics reporting |

`ConnectionLimitEntry` tracks progressive ban tiers (`BanCount`, `LastBanTimeTicks`), DDoS log suppression, and reject/close log throttling per IP.

## Performance Rules

- Socket I/O uses `Span<byte>` and pooled buffers — no `new byte[]` on receive path.
- ConnectionHub sharding eliminates lock contention — do NOT use a single `ConcurrentDictionary`.
- UDP listener MUST validate anti-replay window before any deserialization.
- Session snapshots use cache-aside pattern with strict TTL.

## Anti-Patterns

- Do NOT create sessions on disconnect — create them immediately after handshake.
- Do NOT process UDP packets without HMAC verification first.
- Do NOT use `Thread.Sleep` in listener loops.
- Do NOT bypass `ConnectionHub` for connection management.
