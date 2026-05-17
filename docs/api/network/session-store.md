# Session Store & Service

`ISessionService`, `ISessionFactory`, and `ISessionStore` form the core state management layer responsible for persisting, retrieving, and expiring resumable session data. In the Nalix architecture, a "Session" represents the cryptographic state (Session Token, Symmetric Secret) that allows a client to disconnect and reconnect without performing a full X25519 handshake.

## Source Mapping

- `src/Nalix.Abstractions/Networking/Sessions/ISessionService.cs`
- `src/Nalix.Abstractions/Networking/Sessions/ISessionFactory.cs`
- `src/Nalix.Abstractions/Networking/Sessions/ISessionStore.cs`
- `src/Nalix.Network/Sessions/SessionService.cs`
- `src/Nalix.Network/Sessions/SessionFactory.cs`
- `src/Nalix.Network/Sessions/InMemorySessionStore.cs`

## Why These Types Exist

Maintaining session state across disconnects requires a storage mechanism that is:

- **Decoupled**: Clean separation of concerns between lifecycle policy, serialization snapshots, and low-level data storage.
- **Fast**: Session retrieval happens during the connection "Hot Path" (Resume).
- **Atomic**: Prevents multiple clients from attempting to resume the same session simultaneously.
- **Auto-Cleaning**: Expired sessions must be evicted to prevent memory leaks or replay window bloat.

## Session Persistence Flow

The following diagram illustrates how a session is created during a full handshake and subsequently consumed during a resumption.

```mermaid
sequenceDiagram
    participant C as Client
    participant S as Server
    participant SSV as SessionService
    participant ST as SessionStore

    Note over C,S: Full Handshake (TCP)
    C->>S: Handshake Request (X25519)
    S->>S: Generate SessionToken & Secret
    S->>SSV: SaveSessionAsync(connection)
    SSV->>ST: StoreAsync(SessionEntry)
    S->>C: Handshake Success (Returns Token)

    Note over C,S: Disconnect / Network Drop

    Note over C,S: Resumption (TCP)
    C->>S: ResumeRequest(SessionToken, Nonce)
    S->>SSV: ConsumeAsync(SessionToken)
    SSV->>ST: ConsumeAsync(SessionToken)
    ST-->>SSV: Return Entry & Delete from Store
    SSV-->>S: Return SessionEntry
    
    alt Session Valid
        S->>C: Resume Success
    else Session Expired or Already Consumed
        S->>C: Resume Fail (Full Handshake Required)
    end
```

## Internal Responsibilities (Source-Verified)

### 1. Atomic Consumption (SEC-33)

The most critical method in the lifecycle is `ConsumeAsync(ulong sessionToken)`.

- `SessionService.ConsumeAsync(...)` delegates to the underlying `ISessionStore.ConsumeAsync(...)`.
- The store retrieves the session entry and **immediately removes it** from the storage medium in a single atomic operation (e.g., using `ConcurrentDictionary.TryRemove` in-memory, or a Lua script in Redis).
- This prevents "Resumption Replay" where a stolen token could be used by two different clients to gain access simultaneously. Only the first caller succeeds.

!!! danger "Security Requirement"
    Custom implementations of `ISessionStore` (e.g., Redis implementations) **MUST** implement `ConsumeAsync` as an atomic operation (e.g., using a Lua script in Redis) to comply with SEC-33.

### 2. Active Expiration via Hosted Workers

The `InMemorySessionStore` employs a dual-layered expiration strategy:

- **Active Scavenger (`IHostedWorker`)**: The store directly implements `IHostedWorker`. Its `ExecuteAsync` loop is automatically scheduled by the `SessionService` constructor using the runtime's global `TaskManager`. The scavenger runs a `PeriodicTimer` that ticks every minute, scanning the `ConcurrentDictionary` and evicting expired keys.
- **Lazy Check**: Every time `ConsumeAsync` is called, the TTL is checked immediately. If the session has expired, the entry is behandled as expired, its resources are reclaimed, and it returns `null` even if the active scavenger has not run yet.

### 3. Session Entry Pooling

To keep the resumption path zero-allocation, `SessionEntry` objects are tracked by the `ObjectPoolManager`. When a session is removed or expires, the system calls `entry.Return()` to reclaim the resources.

## Public APIs

### `ISessionService`
- `SaveSessionAsync(connection)`: Persists the session for the specified connection, enforcing handshake-state and minimum-attribute policies. **Preferred path for normal unregister flows.**
- `ConsumeAsync(token)`: Atomically retrieves and removes the session. **Primary method for Resumption logic.**

### `ISessionStore`
- `StoreAsync(entry)`: Direct, low-level persistence of a `SessionEntry`, bypassing connection-level policy checks.
- `ConsumeAsync(token)`: Atomically retrieves and removes a session from storage.

## Configuration

Control the session lifecycle via `SessionStoreOptions`:

| Option | Description | Typical Value |
| :---: | :---: | :---: |
| `SessionTtl` | How long a session remains resumable after creation. | `00:30:00` (30 minutes) |
| `AutoSaveOnUnregister` | Whether sessions are automatically saved when a connection is unregistered. | `true` |
| `MinAttributesForPersistence` | Minimum attribute count required to persist a session (anti-DDoS filter). Persistence is skipped when `Attributes.Count <= MinAttributesForPersistence`. | `4` |

!!! tip
    For multi-node (Distributed) deployments, you should replace the default `InMemorySessionStore` with a custom implementation bridging to a persistent store like Redis or Aerospike to ensure session state is shared across all shards.

## Related Information Paths

- [Handshake Protocol](../security/handshake.md)
- [Session Resumption](../security/session-resume.md)
- [Snowflake Identifiers (ulong)](../framework/snowflake.md)
- [Object Pooling](../framework/memory/object-pooling.md)
- [Object Map](../framework/memory/object-map.md)
