# PacketContext — Request Context for Packet Handlers

`PacketContext<TPacket>` is the object passed into packet handlers. It holds the current packet, connection, metadata, cancellation, and a sender that applies encryption/compression according to handler attributes.

- **Namespace:** `Nalix.Network.Routing`
- **Pooled:** Yes (object pooling for high throughput).

---

## Properties

| Property            | Description                                                                                            |
|---------------------|--------------------------------------------------------------------------------------------------------|
| `Packet`            | The current packet being processed.                                                                    |
| `Connection`        | The client connection (`IConnection`).                                                                 |
| `Attributes`        | Packet metadata (opcode, encryption, permission, rate limit, timeout, etc.).                           |
| `Sender`            | `IPacketSender<TPacket>` — use to send packets with automatic encrypt/compress per handler attributes. |
| `CancellationToken` | Cancellation for this request.                                                                         |
| `SkipOutbound`      | When true, outbound middleware is skipped (e.g. after sending via `Sender`).                           |

---

## When to Use Return vs Sender

- **Return value:** Handler returns a packet (or `Task<T>`) → pipeline runs outbound middleware (e.g. wrap/encrypt) and sends the result. Use for a single response tied to the request.
- **Sender:** Use `context.Sender.SendAsync(packet, ct)` when you want to send one or more packets from inside the handler with the same encryption/compression rules, without going through the normal return path.

---

## Example

```csharp
[PacketOpcode(0x1002)]
public async ValueTask OnRequest(PacketContext<MyPacket> context, CancellationToken ct)
{
    var response = BuildResponse(context.Packet);
    await context.Sender.SendAsync(response, ct);
}
```

---

## See Also

- [Packet Dispatch & Handler](./PacketDispatchChannel.md)
- [Packet Attributes](./PacketAttributes.md)
- [Middleware](../Middleware/README.md)
