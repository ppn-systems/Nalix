# Session Store

`Nalix.Network.Sessions` provides the default runtime session-store implementations used by the network layer.

## Source mapping

- `src/Nalix.Network/Sessions/SessionStoreBase.cs`
- `src/Nalix.Network/Sessions/InMemorySessionStore.cs`
- `src/Nalix.Network/Options/SessionStoreOptions.cs`

## Main types

- `SessionStoreBase`
- `InMemorySessionStore`
- `SessionStoreOptions`

## SessionStoreBase

`SessionStoreBase` is the shared base class for session-store implementations.

It implements the `ISessionStore` contract from `Nalix.Common` and provides the common session creation flow:

- generates a new session token
- captures the connection state into `SessionSnapshot`
- copies the current connection attributes
- records the token back onto the live connection

### Common pitfalls

- treating `CreateSession(...)` as persistence; it only builds the entry
- assuming the base class stores or deletes anything by itself
- forgetting that the base class reads retention settings from configuration

## InMemorySessionStore

`InMemorySessionStore` is the built-in single-node implementation.

It is backed by a `ConcurrentDictionary<UInt56, SessionEntry>` and:

- stores entries by session token
- removes entries on explicit delete
- lazily evicts expired entries on retrieval

### Common pitfalls

- using the in-memory store in a multi-node deployment without a shared backend
- assuming retrieval never removes entries; expired entries are pruned during lookup
- forgetting that removed or expired entries are returned to the pool

## SessionStoreOptions

`SessionStoreOptions` controls how long resumable sessions are retained before expiration.

### Common pitfalls

- setting the TTL longer than the actual security policy allows
- assuming TTL is refreshed automatically when a session is only read

## Related APIs

- [Session Contracts](../common/session-contracts.md)
- [Connection Contracts](../common/connection-contracts.md)
- [Session Resume](../security/session-resume.md)
