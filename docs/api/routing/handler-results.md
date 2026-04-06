# Handler Return Types

Nalix.Network lets handler methods return more than just `void`. The dispatcher resolves the handler return type and chooses a matching result handler at runtime.

## Source mapping

- `src/Nalix.Network/Internal/Results/ReturnTypeHandlerFactory.cs`
- `src/Nalix.Network/Internal/Results/*`

## Supported return shapes

The factory currently supports:

| Return type | Behavior |
|---|---|
| `void` | No response is sent. |
| `Task` | Awaits completion, no payload response. |
| `ValueTask` | Awaits completion, no payload response. |
| `TPacket` | Serializes the packet and sends it over TCP. |
| `Task<TPacket>` | Awaits, then sends the packet. |
| `ValueTask<TPacket>` | Awaits, then sends the packet. |
| `byte[]` | Sends raw bytes directly. |
| `Memory<byte>` / `ReadOnlyMemory<byte>` | Sends raw memory directly. |
| `Task<byte[]>`, `Task<Memory<byte>>`, etc. | Awaits, then uses the matching inner handler. |

Unsupported return types fall back to `UnsupportedReturnHandler`.

## Practical guidance

Use:

- `void`, `Task`, `ValueTask` when your handler sends manually through `context.Sender`
- `TPacket` or `Task<TPacket>` for a normal single reply
- `byte[]` or `Memory<byte>` when you already own the serialized payload

## Important note about outbound flow

Two reply styles exist:

1. **return a value**
   - the dispatch return pipeline handles the response
2. **send manually**
   - call `context.Sender.SendAsync(...)`
   - this is the better choice for multiple replies or finer control

`PacketContext.SkipOutbound` exists so the dispatch pipeline can skip normal outbound middleware when appropriate.

## Example

```csharp
[PacketOpcode(0x1201)]
public static Control HandlePing(Control packet, IConnection connection)
{
    return packet;
}
```

## Related APIs

- [Packet Context](./packet-context.md)
- [Packet Dispatch](./packet-dispatch.md)
