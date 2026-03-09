# Session Store

`Nalix.Network.Sessions` provides server-side session persistence abstractions and the default in-memory implementation used by resume flows.

## Audit Summary

- Existing page had correct architecture direction but needed stronger mapping to concrete methods and lifecycle semantics.
- Source mapping remains valid.

## Missing Content Identified

- Explicit distinction between session creation and persistence.
- Clear behavior notes for in-memory expiration/removal.

## Improvement Rationale

This prevents misuse in multi-node production deployments.

## Source Mapping

- `src/Nalix.Network/Sessions/SessionStoreBase.cs`
- `src/Nalix.Network/Sessions/InMemorySessionStore.cs`
- `src/Nalix.Network/Options/SessionStoreOptions.cs`

## Core Types

### `SessionStoreBase`

Abstract base implementing shared `ISessionStore` behavior:

- `CreateSession(IConnection connection)` builds `SessionEntry` snapshot from current connection state.
- `StoreAsync(...)`, `RetrieveAsync(...)`, `RemoveAsync(...)` are abstract persistence operations.

### `InMemorySessionStore`

Default single-node implementation backed by `ConcurrentDictionary<UInt56, SessionEntry>`:

- `StoreAsync` upserts entry by session token.
- `RetrieveAsync` lazily removes expired entries before returning.
- `RemoveAsync` deletes entry and returns pooled resources.

### `SessionStoreOptions`

- `SessionTtl` defines resume-session retention duration.

## Best Practices

- Use `InMemorySessionStore` for single-node/local deployments.
- Use distributed backing store for multi-node resume semantics.
- Keep `SessionTtl` aligned with security and rotation policy.

## Related APIs

- [Connection Hub](./connection/connection-hub.md)
- [Session Resume](../security/session-resume.md)
- [Session Contracts](../common/session-contracts.md)
