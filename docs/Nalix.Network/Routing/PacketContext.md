# PacketContext — Pooled per-request packet state

`PacketContext<TPacket>` is the request-scoped object used by context-aware handlers in Nalix.Network. It carries the packet, connection, resolved metadata, request cancellation token, and a pooled sender that can emit outbound packets using the current handler rules.

## Mapped source

- `src/Nalix.Network/Routing/PacketContext.cs`

## State carried by the context

| Property | Purpose |
|---|---|
| `Packet` | Current deserialized packet instance. |
| `Connection` | Current `IConnection`. |
| `Attributes` | `PacketMetadata` resolved for the handler. |
| `CancellationToken` | Request-scoped cancellation token. |
| `SkipOutbound` | Internal flag that skips normal outbound middleware after the handler finishes. |
| `Sender` | Pooled `IPacketSender<TPacket>` resolved during initialization. |

## Pooling behavior

- Implements `IPoolable`.
- Uses `ObjectPoolManager`.
- Static initialization preallocates and sets max capacity based on `PoolingOptions`.
- `Initialize(...)` moves the object into the in-use state and rents a `PacketSender<TPacket>`.
- `Reset()` returns the rented sender and clears packet / connection / metadata state.
- `Return()` only returns the instance when the state transitions from `IN_USE` to `RETURNED`, preventing double-return races.

## Handler guidance

Use a context-based handler when you need metadata or manual sending:

```csharp
[PacketOpcode(0x1002)]
public async ValueTask Handle(PacketContext<MyPacket> context, CancellationToken ct)
{
    await context.Sender.SendAsync(BuildReply(context.Packet), ct);
}
```

Returning a packet from the handler uses the normal return-type pipeline. Sending through `context.Sender` is the explicit path for immediate or multiple replies.

## Notes

- `Sender` is required; initialization throws if the pool returns `null`.
- `SkipOutbound` is internal because it is controlled by the dispatcher / return pipeline, not by external callers.

## See also

- [PacketDispatchChannel](./PacketDispatchChannel.md)
- [PacketAttributes](./PacketAttributes.md)
