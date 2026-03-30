# Session Contracts

`Nalix.Abstractions.Networking.Sessions` defines the contracts used to capture and persist resumable session state.

## Source mapping

- `src/Nalix.Abstractions/Networking/Sessions/ISessionStore.cs`
- `src/Nalix.Abstractions/Networking/Sessions/SessionEntry.cs`
- `src/Nalix.Abstractions/Networking/Sessions/SessionSnapshot.cs`

## Main types

- `ISessionStore`
- `SessionEntry`
- `SessionSnapshot`

## Public members at a glance

| Type | Public members |
|---|---|
| `ISessionStore` | `CreateSession(...)`, `StoreAsync(...)`, `RetrieveAsync(...)`, `RemoveAsync(...)` |
| `SessionEntry` | `Snapshot`, `ConnectionId`, `Return()` |
| `SessionSnapshot` | `SessionToken`, `CreatedAtUnixMilliseconds`, `ExpiresAtUnixMilliseconds`, `Secret`, `Algorithm`, `Level`, `Attributes` |

## ISessionStore

`ISessionStore` is the persistence contract for resumable session state.

It is responsible for:

- creating a session entry from a live connection
- persisting the session entry
- looking up a stored session by token
- removing a stored session when it is no longer valid

### Common pitfalls

- treating the store as an in-memory cache only when your deployment needs distributed persistence
- returning stale `SessionEntry` values after a connection has already been replaced
- forgetting to remove expired or invalidated sessions during disconnect or rotation

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
