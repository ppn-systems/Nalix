# Session Store Options

`SessionStoreOptions` controls resumable-session retention and the persistence policy used when connections leave the registry.

## Source Mapping

- `src/Nalix.Runtime/Options/SessionStoreOptions.cs`
- `src/Nalix.Runtime/Sessions/SessionService.cs`
- `src/Nalix.Runtime/Sessions/InMemorySessionStore.cs`
- `src/Nalix.Runtime/Sessions/SessionPersistenceObserver.cs`
- `src/Nalix.Hosting/Bootstrap.cs`

## Defaults and Validation

| Property | Default | Validation | Runtime consumer |
| --- | ---: | --- | --- |
| `SessionTtl` | `00:01:00` | Required and `> TimeSpan.Zero` | `ISessionFactory.CreateSession(...)` sets `ExpiresAtUnixMilliseconds`. |
| `MinAttributesForPersistence` | `10` | `0..int.MaxValue` | `SessionService.SaveSessionAsync(IConnection)` skips low-value sessions. |

`Validate()` uses manual range checks and throws `ArgumentOutOfRangeException` when constraints are violated. It rejects non-positive `SessionTtl` values and negative `MinAttributesForPersistence`.

## Hosting Initialization

`Bootstrap.Initialize()` materializes this option set during server startup:

```csharp
_ = ConfigurationManager.Instance.Get<Nalix.Runtime.Options.SessionStoreOptions>();
```

This ensures the active configuration file contains the resumable-session retention and persistence policy alongside the other network-level options.

## Session Creation Flow

```mermaid
flowchart TD
    A["ConnectionRegistry raises ConnectionUnregistered"] --> B["SessionPersistenceObserver receives OnConnectionClosed"]
    B --> C["Starts background SaveSessionAsync(connection)"]
    C --> D{"HandshakeEstablished attribute == true?"}
    D -->|No| Z["Skip session persistence"]
    D -->|Yes| E{"Attributes.Count > MinAttributesForPersistence?"}
    E -->|No| Z
    E -->|Yes| F["ISessionFactory.CreateSession copies connection attributes"]
    F --> G["ExpiresAt = now + SessionTtl"]
    G --> H["ISessionStore.StoreAsync(session)"]
```

`ISessionFactory.CreateSession(...)` snapshots the connection into a `SessionEntry`:

- `SessionToken` is derived from `connection.ID.ToUInt64()`.
- `CreatedAtUnixMilliseconds` uses `Clock.UnixMillisecondsNow()`.
- `ExpiresAtUnixMilliseconds` is `now + SessionTtl.TotalMilliseconds`.
- `Secret`, `Algorithm`, and `Level` are copied from the connection.
- Connection attributes are copied into a rented `ObjectMap<string, object>`.

## Automatic Persistence Contract

Automatic persistence is decoupled from the connection hub by `SessionPersistenceObserver`, which subscribes to the `ConnectionUnregistered` event on the `IConnectionHub` and triggers `SaveSessionAsync(...)` in a fire-and-forget background task. Persistence is guided by the following conditions:

1. The connection must not be disposed.
2. `ConnectionAttributes.HandshakeEstablished` must exist and be `true`.
3. `connection.Attributes.Count` must be greater than `MinAttributesForPersistence`.

The attribute threshold is an anti-abuse filter. It avoids retaining handshake-only or nearly empty sessions that could otherwise be created in bulk by short-lived connections.

If `SaveSessionAsync(...)` completes synchronously and successfully, the session is owned by the store. If storing throws immediately, the newly created `SessionEntry` is returned. If storing completes asynchronously, the task is awaited without blocking unregistration.

## In-Memory Store Behavior

`InMemorySessionStore` is the default store used by `SessionService` when no custom `ISessionStore` is injected.

### Storage and Replacement

`StoreAsync(...)` uses a `ConcurrentDictionary<ulong, SessionEntry>` keyed by the session token:

- First insert wins when the token is absent.
- Storing the same `SessionEntry` reference again is a no-op.
- Replacing an existing token uses `TryUpdate(...)` and returns the old entry.

### Expiration

Expiration is enforced in two places:

- A background scavenger implemented via `IHostedWorker` runs every minute and removes expired entries.
- `ConsumeAsync(...)` also performs lazy TTL checks.

Expired entries are removed from the dictionary and returned to their backing pools through `SessionEntry.Return()`.

### Consume Semantics

`ConsumeAsync(...)` uses `ConcurrentDictionary.TryRemove(...)`, so a session token is atomic one-shot state: only one concurrent caller can successfully consume and resume it. Expired consumed entries are returned and reported as `null`.

### Disposal

`SessionService.Dispose()` disposes the scavenger worker handle (`IWorkerHandle`) scheduled on `TaskManager` if `InMemorySessionStore` was used as the backing store, cleanly canceling the background execution loop.

## Memory Management Notes

- Session snapshots own a rented `ObjectMap<string, object>` containing copied connection attributes.
- Store implementations must return replaced, removed, expired, or failed-to-store `SessionEntry` instances.
- The default in-memory store returns entries on replacement, removal, lazy expiration, consumption expiration, and background scavenging.
- Automatic persistence avoids blocking connection unregistration on asynchronous stores.

## Tuning Guidance

- Keep `SessionTtl` aligned with authentication token lifetime and key rotation.
- Lower `SessionTtl` to reduce retained state and replay-token lifetime.
- Raise `MinAttributesForPersistence` when public endpoints see many handshake-only disconnects.
- Lower `MinAttributesForPersistence` only if legitimate resumable sessions carry few attributes.
- Use a distributed `ISessionStore` for multi-node deployments and preserve the same TTL and one-shot consume semantics.

## Related APIs

- [Session Store](../../network/session-store.md)
- [Session Resume](../../security/session-resume.md)
- [Network Options](./options.md)
