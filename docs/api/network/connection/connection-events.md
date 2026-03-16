# Connection Events

Connection event argument types carry structured data between transport callbacks and protocol/runtime layers.

## Audit Summary

- Existing page conceptually correct; needed precise boundary between connection event args and hub-capacity event/delegate usage.

## Missing Content Identified

- Explicit mention that `ConnectionEventArgs` is pooled and reused.
- Clear source mapping and related event producers.

## Improvement Rationale

Clear event-payload semantics reduce callback misuse and lifecycle bugs.

## Source Mapping

- `src/Nalix.Network/Connections/Connection.EventArgs.cs`
- `src/Nalix.Network/Connections/Connection.Hub.cs`

## `ConnectionEventArgs`

Used by connection-level events (`OnCloseEvent`, `OnProcessEvent`, `OnPostProcessEvent`).

Key members:

- `Connection`
- `Lease`
- `NetworkEndpoint`
- lifecycle helpers: `Initialize(...)`, `ExchangeLease(...)`, `Dispose()`

`ConnectionEventArgs` implements `IPoolable`; instances are returned to pool after use.

## Hub Capacity Events

`ConnectionHub` exposes:

- `CapacityLimitReached` delegate event

Use this event to react to registration admission pressure according to configured drop policy.

## Best Practices

- Treat `Lease` ownership carefully in callback pipelines.
- Do not cache pooled event arg instances beyond callback scope.
- Use hub capacity callbacks for operational monitoring and alerting.

## Related APIs

- [Connection](./connection.md)
- [Connection Hub](./connection-hub.md)
- [Protocol](../protocol.md)
