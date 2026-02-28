# Handler Return Types

In Nalix, packet handlers are not restricted to `void`. The runtime includes a zero-allocation `ReturnTypeHandlerFactory` that dynamically resolves and executes response logic based on the method signature. This allows for a clean, expressive style for both synchronous and asynchronous message exchanges.

## Supported Return Types

The following table outlines the built-in return handlers and their resulting behaviors.

| Return Type | Dispatched Behavior |
|---|---|
| `void` | No response is sent to the client. Ideal for one-way "fire and forget" messages. |
| `Task` / `ValueTask` | Awaits completion of the handler logic without sending a payload. |
| `TPacket` | Serializes the returned packet and sends it back over the same transport. |
| `byte[]` | Sends the raw byte array directly to the client as a binary payload. |
| `Memory<byte>` | Sends the memory slice as a binary payload (zero-copy supported). |
| `ReadOnlyMemory<byte>` | Sends the read-only slice as a binary payload. |
| `Task<T>` / `ValueTask<T>` | Awaits the task, then processes the result `T` using the appropriate handler above. |

## Role and Design

The return type system is designed to minimize boiler-plate while maintaining performance.

- **Zero-Allocation Dispatch**: The `ReturnTypeHandlerFactory<TPacket>` uses immutable frozen dictionaries and concurrent maps to resolve handlers in $O(1)$ time without runtime allocations.
- **Recursive Resolution**: Supports complex async wrappers like `Task<ValueTask<TPacket>>` by recursively unwrapping types until a terminal handler is found.
- **Unified Pipeline**: Returns are automatically passed through the outbound middleware pipeline unless specifically skipped.

## Implementation Styles

### 1. The Functional Style (Single Reply)
Best for simple request-response flows where the handler's only job is to provide one answer.

```csharp
[PacketOpcode(0x1001)]
public TPacket Handle(TPacket request, IConnection conn)
{
    // Simply return the response; the runtime handles serialization and sending.
    return new HandshakeResponse { Success = true };
}
```

### 2. The Context Style (Multiple Replies)
Best for complex orchestration, streaming, or manual kontrol over the response timing. In this style, the return type is usually `void` or `Task`.

```csharp
[PacketOpcode(0x1002)]
public async ValueTask Handle(IPacketContext<TPacket> context, CancellationToken ct)
{
    // Send multiple packets or conditional responses.
    await context.Sender.SendAsync(new StatusUpdate { Progress = 50 }, ct);
    
    // logic...
    
    await context.Sender.SendAsync(new StatusUpdate { Progress = 100 }, ct);
}
```

## Important Considerations

- **Outbound Middleware**: Values returned from handlers travel through the `OutboundMiddleware` pipeline. If you need to bypass this, use `context.SkipOutbound = true`.
- **Exception Handling**: If a handler throws an exception, the return value is ignored.
- **Unsupported Types**: If a handler returns a type that is not registered, the `UnsupportedReturnHandler` will log a warning and drop the result.

## Related APIs

- [Packet Context](./packet-context.md)
- [Packet Dispatch](./packet-dispatch.md)
- [Middleware Pipeline](../middleware/pipeline.md)
