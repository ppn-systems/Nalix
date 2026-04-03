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
| `string` | Encodes as UTF-8 text packet(s), choosing the smallest text frame that fits. |
| `Task<string>` / `ValueTask<string>` | Awaits, then sends text packet(s). |
| `byte[]` | Sends raw bytes directly. |
| `Memory<byte>` / `ReadOnlyMemory<byte>` | Sends raw memory directly. |
| `Task<byte[]>`, `Task<Memory<byte>>`, etc. | Awaits, then uses the matching inner handler. |

Unsupported return types fall back to `UnsupportedReturnHandler`.

## Practical guidance

Use:

- `void`, `Task`, `ValueTask` when your handler sends manually through `context.Sender`
- `TPacket` or `Task<TPacket>` for a normal single reply
- `string` when you intentionally want a text-frame response
- `byte[]` or `Memory<byte>` when you already own the serialized payload

## Important note about outbound flow

Two reply styles exist:

1. **return a value**
   - the dispatch return pipeline handles the response
2. **send manually**
   - call `context.Sender.SendAsync(...)`
   - this is the better choice for multiple replies or finer control

`PacketContext.SkipOutbound` exists so the dispatch pipeline can skip normal outbound middleware when appropriate.

## How strings are handled

String results are not sent as plain .NET strings on the wire.

The current implementation:

- UTF-8 encodes the content
- chooses the smallest registered text frame type that fits
- falls back to chunking across multiple packets when necessary
- preserves Unicode rune boundaries while splitting
- uses pooled `Text256`, `Text512`, and `Text1024` packet instances for text replies

## Example

```csharp
[PacketOpcode(0x1201)]
public static Control HandlePing(Control packet, IConnection connection)
{
    return packet;
}

[PacketOpcode(0x1202)]
public static async Task<string> HandleTextAsync(Control packet, IConnection connection)
{
    await Task.Delay(10);
    return "pong";
}
```

## Related APIs

- [Packet Context](./packet-context.md)
- [Packet Dispatch](./packet-dispatch.md)
