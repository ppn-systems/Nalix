# Session Contracts

`Nalix.Abstractions.Networking.Sessions` defines the contracts used to capture and persist resumable session state.

## Source mapping

- `src/Nalix.Abstractions/IHostedWorker.cs`
- `src/Nalix.Abstractions/Networking/Sessions/ISessionService.cs`
- `src/Nalix.Abstractions/Networking/Sessions/ISessionFactory.cs`
- `src/Nalix.Abstractions/Networking/Sessions/ISessionStore.cs`
- `src/Nalix.Abstractions/Networking/Sessions/SessionEntry.cs`
- `src/Nalix.Abstractions/Networking/Sessions/SessionSnapshot.cs`

## Main types

- `ISessionService`
- `ISessionFactory`
- `ISessionStore`
- `IHostedWorker`
- `SessionEntry`
- `SessionSnapshot`

## Public members at a glance

| Type | Public members |
|---|---|
| `ISessionService` | `SaveSessionAsync(...)`, `ConsumeAsync(...)` |
| `ISessionFactory` | `CreateSession(...)` |
| `ISessionStore` | `StoreAsync(...)`, `ConsumeAsync(...)` |
| `IHostedWorker` | `ExecuteAsync(...)` |
| `SessionEntry` | `Snapshot`, `ConnectionId`, `Return()` |
| `SessionSnapshot` | `SessionToken`, `CreatedAtUnixMilliseconds`, `ExpiresAtUnixMilliseconds`, `Secret`, `Algorithm`, `Level`, `Attributes` |

## ISessionService

`ISessionService` coordinates the lifecycle of session persistence. It is the primary high-level interface consumed by `ConnectionHub` and standard packet handlers to store and resume sessions.

It is responsible for:

- coordinating session saving by applying connection-level policies (e.g., checking if the handshake was established, checking attribute count thresholds)
- calling `ISessionFactory` to construct a new `SessionEntry` snapshot from a live connection
- storing the snapshot into the underlying storage engine (`ISessionStore`)
- coordinating background workers (e.g., active scavengers) if the backing store requires scheduling

### Common pitfalls

- calling `SaveSessionAsync` on unestablished or disposed connections
- forgetting that `ConsumeAsync` is a one-shot operation that removes the session state from the store upon invocation

## ISessionFactory

`ISessionFactory` is responsible for capturing live connection state and packaging it into a serializable, pooled `SessionEntry`.

It is responsible for:

- allocating and initializing a `SessionEntry` from an active connection
- copying security secrets, algorithms, authorization levels, and custom attributes into the entry's snapshot
- capturing TCP and UDP sequence numbers to preserve framing boundaries upon reconnection

## ISessionStore

`ISessionStore` represents the low-level state persistence engine. It is focused strictly on CRUD/atomic operations, with all connection-level policy checks delegated upward to `ISessionService`.

It is responsible for:

- persisting the `SessionEntry` directly to the storage medium (database, in-memory, or distributed cluster like Redis)
- atomically retrieving and removing the entry via `ConsumeAsync` to guarantee single-use session tokens

### Common pitfalls

- treating the store as a cache and forgetting that custom implementations (e.g., Redis) **must** implement `ConsumeAsync` atomically to prevent resumption replay exploits
- forgetting to return the `SessionEntry` resources to the object pool (`entry.Return()`) if a store operation fails or a session expires

## IHostedWorker

`IHostedWorker` represents a long-running background task managed by the runtime's scheduler (`TaskManager`).

It is responsible for:

- executing background tasks (e.g., periodic scavenging of expired session entries)
- integrating seamlessly with the runtime's lifecycle management and cancellation tokens

## SessionEntry

`SessionEntry` wraps a reusable session snapshot together with runtime connection identity.

Use it when you need to move session state between the live connection and the persistent store.

### Common pitfalls

- mutating the snapshot without returning the entry to the pool when the implementation expects reuse
- assuming the `ConnectionId` stays valid after a reconnect or resume attempt

## SessionSnapshot

`SessionSnapshot` is the serializable session payload used by the store.

It contains the state required to resume a connection, including:

- session token
- creation and expiration timestamps
- secret and algorithm information
- security level and attribute data

### Common pitfalls

- persisting more runtime state than the snapshot is meant to carry
- rotating the token without updating the stored snapshot atomically

## Related APIs

- [Connection Contracts](./connection-contracts.md)
- [Session Resume](../security/session-resume.md)
- [Handshake Protocol](../security/handshake.md)
