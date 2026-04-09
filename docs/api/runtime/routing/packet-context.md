# Packet Context

`PacketContext<TPacket>` is the pooled per-request implementation of `IPacketContext<TPacket>` used by context-aware handlers in Nalix.Network. It carries the packet, connection, resolved metadata, cancellation token, and a pooled sender that can emit outbound packets using the current handler rules.
The generic `TPacket` can be a built-in packet or one of your own custom packet types.

## Source mapping

- `src/Nalix.Runtime/Dispatching/PacketContext.cs`

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

- implements `IPoolable`
- uses `ObjectPoolManager`
- preallocates and sets pool limits based on `PoolingOptions`
- `Initialize(...)` moves the object into the in-use state and rents a `PacketSender<TPacket>`
- `Reset()` returns the rented sender and clears packet, connection, and metadata state
- `Return()` guards against double-return races

## Handler guidance

Use a context-based handler when you need metadata or manual sending, especially when the packet type is custom and needs the current request metadata:

## Example

```csharp
[PacketOpcode(0x1002)]
public async ValueTask Handle(IPacketContext<Handshake> context, CancellationToken ct)
{
    await context.Sender.SendAsync(
        new Handshake(0x1003, HandshakeStage.SERVER_FINISH, [], [], []),
        ct);
}
```

Custom packet example:

```csharp
[PacketOpcode(0x2201)]
public async ValueTask Handle(PacketContext<InvoicePacket> context, CancellationToken ct)
{
    await context.Sender.SendAsync(context.Packet, ct);
}
```

Returning a packet uses the normal return-type pipeline. Sending through `context.Sender` is the explicit path for immediate or multiple replies.

## Related APIs

- [Packet Dispatch](./packet-dispatch.md)
- [Packet Attributes](./packet-attributes.md)
- [Packet Metadata](./packet-metadata.md)
- [Packet Sender](./packet-sender.md)
- [Handler Results](./handler-results.md)
- [Connection](../../network/connection/connection.md)
