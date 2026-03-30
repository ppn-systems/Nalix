# Connection Events

Nalix.Network exposes event argument types for connection lifecycle and hub-capacity events.

## Source mapping

- `src/Nalix.Network/Connections/Connection.EventArgs.cs`
- `src/Nalix.Network/Connections/Connection.Hub.EventArgs.cs`

## ConnectionEventArgs

`ConnectionEventArgs` is the standard event payload for connection events.

It provides:

- `Connection`
- `Lease`
- `NetworkEndpoint`

The current implementation is pool-aware and implements `IPoolable`.

## ConnectionHubEventArgs

`ConnectionHubEventArgs` is raised when hub capacity-related limits are hit.

It provides:

- active `DropPolicy`
- current and max connection counts
- triggering connection ID
- textual reason
- a statistics snapshot

## When you care about these types

Use them when you:

- subscribe to listener or connection lifecycle events
- handle capacity notifications from `ConnectionHub`
- want structured data instead of parsing log output

## Example

```csharp
connection.OnCloseEvent += (_, args) =>
{
    Console.WriteLine($"closed: {args.Connection.ID} from {args.NetworkEndpoint}");
};

hub.CapacityLimitReached += (_, args) =>
{
    Console.WriteLine($"hub limit hit: {args.CurrentConnections}/{args.MaxConnections}");
};
```

## Related APIs

- [Connection](./connection.md)
- [Connection Hub](./connection-hub.md)
