# Handler Return Types

Handler return types are resolved at compile time by the `PacketHandlerGenerator` source generator. The generator inspects each handler method signature and emits an optimized invoker delegate that handles the return value without runtime reflection.

## Overview

Nalix allows you to choose the most natural return style for your handler logic — synchronous, asynchronous, or raw binary data — while keeping outbound transport logic centralized and consistent.

## Source Mapping

- `analyzers/Nalix.Analyzers.Generators/PacketHandlerGenerator.cs`
- `src/Nalix.Runtime/Dispatching/Options/PacketDispatchOptions.PublicMethods.cs`

## Supported Shapes

| Return type shape | Behavior |
| --- | --- |
| `void` | No payload is sent. |
| `Task` / `ValueTask` | Awaited to completion; no payload is sent. |
| `TPacket` (or any `IPacket`) | Sent as a packet response through the runtime sender flow. |
| `byte[]` | Sent as raw payload. |
| `Memory<byte>` / `ReadOnlyMemory<byte>` | Sent as raw payload memory. |
| `Task<T>` / `ValueTask<T>` | Awaited, then the inner `T` is resolved as one of the above shapes. |

## Why It Exists

Handlers should be able to return responses naturally (sync or async) while the runtime keeps sending behavior centralized and consistent.

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
