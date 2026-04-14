# Session Store Options

`SessionStoreOptions` controls retention behavior for resumable sessions in the network layer.
It exists so resume-token validity can be tuned independently of listener and dispatch settings.

## Audit Summary

- `SessionStoreOptions` exists in source but had no dedicated API page.
- This created a gap in the Network Options reference set.

## Missing Content Identified

- No standalone documentation for `SessionTtl`.
- No guidance on aligning TTL with session resume strategy.

## Improvement Rationale

A dedicated page makes session-retention policy explicit and easier to review during production hardening.

## Source Mapping

- `src/Nalix.Network/Options/SessionStoreOptions.cs`
- `src/Nalix.Network/Sessions/SessionStoreBase.cs`
- `src/Nalix.Network/Sessions/InMemorySessionStore.cs`

## Type and Responsibility

`SessionStoreOptions` (`Nalix.Network.Options`) defines configuration for resumable-session storage policy.
It is consumed by session-store implementations rather than packet dispatch or middleware components.

## Properties

| Property | Type | Default | Purpose |
|---|---|---:|---|
| `SessionTtl` | `TimeSpan` | `00:30:00` | Expiration window for inactive resumable sessions. |

## Usage Notes

- Longer `SessionTtl` improves reconnect tolerance for unstable client networks.
- Shorter `SessionTtl` reduces retained resume state and narrows replay window duration.
- For multi-node deployments, ensure distributed session storage uses the same effective TTL policy.

## Best Practices

- Keep `SessionTtl` consistent with auth token lifetime and key-rotation policy.
- Verify that cleanup/expiration behavior matches client reconnect expectations.
- Load-test resume flows at your chosen TTL boundary conditions.

## Related APIs

- [Session Store](../session-store.md)
- [Session Resume](../../security/session-resume.md)
- [Network Options](./options.md)
