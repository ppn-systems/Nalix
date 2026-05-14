# Handler Return Types

Nalix runtime resolves handler return types through internal return handlers during dispatch execution.

## Overview

Nalix runtime resolves handler return types through internal return handlers during dispatch execution. This allows you to choose the most natural return style for your logic—whether synchronous, asynchronous, or even returning raw binary data—while keeping the outbound transport logic centralized and consistent.

## Source Mapping

- `src/Nalix.Runtime/Internal/Results/ReturnTypeHandlerFactory.cs`
- `src/Nalix.Runtime/Internal/Results/Packet/PacketReturnHandler.cs`
- `src/Nalix.Runtime/Internal/Results/Task`
- `src/Nalix.Runtime/Internal/Results/Memory`
- `src/Nalix.Runtime/Internal/Results/Primitives/ByteArrayReturnHandler.cs`
- `src/Nalix.Runtime/Internal/Results/Void/VoidReturnHandler.cs`

## Supported Shapes

| Return type shape | Runtime behavior |
| --- | --- |
| `void` | No payload is sent. |
| `Task` / `ValueTask` | Await completion, no payload is sent. |
| `TPacket` (or any `IPacket`) | Sent as packet response through runtime sender flow. Supports both classes and structs (e.g., `MemoryPacket`). |
| `byte[]` | Sent as raw payload. |
| `Memory<byte>` / `ReadOnlyMemory<byte>` | Sent as raw payload memory. |
| `Task<T>` / `ValueTask<T>` | Awaited, then resolved again as type `T`. `T` must be a supported shape (`IPacket`, `byte[]`, `Memory<byte>`, `ReadOnlyMemory<byte>`). |

## Why It Exists

Handlers should be able to return responses naturally (sync or async) while runtime keeps sending behavior centralized and consistent.

## Practical Examples

```csharp
using System.Threading.Tasks;
using Nalix.Abstractions.Networking.Packets;

[PacketOpcode(0x1001)]
public static LoginResponse Handle(LoginRequest request) => new();

[PacketOpcode(0x1002)]
public static async Task<LoginResponse> HandleAsync(LoginRequest request)
{
    await Task.Yield();
    return new LoginResponse();
}

[PacketOpcode(0x1003)]
public static ValueTask HandleNoReply(IPacketContext<LoginRequest> context)
    => ValueTask.CompletedTask;
```

For detailed implementation patterns and error handling, see the [Implementing Packet Handlers](../../../guides/application/packet-handlers.md) guide.

## Best Practices

- Prefer returning packet types for simple request/response handlers.
- Use `IPacketContext<TPacket>.Sender` when you need multiple replies or custom send timing.
- Keep return types explicit and consistent across a controller.

## Related APIs

- [Packet Context](./packet-context.md)
- [Packet Dispatch](./packet-dispatch.md)
- [Packet Sender](./packet-sender.md)
