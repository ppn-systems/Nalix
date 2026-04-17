# Packet Dispatch Options

`PacketDispatchOptions<TPacket>` defines how `PacketDispatchChannel` resolves handlers, executes middleware, and sizes its worker-loop behavior.

## Source mapping

- `src/Nalix.Runtime/Dispatching/PacketDispatchOptions.cs`
- `src/Nalix.Runtime/Dispatching/PacketDispatchOptions.PublicMethods.cs`
- `src/Nalix.Runtime/Dispatching/PacketDispatchOptions.Execution.cs`

## Type summary

- Generic constraint: `where TPacket : IPacket`
- Purpose: registration-time configuration for handler table, packet middleware, and buffer middleware.

## Public properties

| Property | Meaning | Default |
|---|---|---:|
| `NetworkPipeline` | Buffer middleware pipeline (`INetworkBufferMiddleware`) executed before deserialization. | initialized |
| `Logging` | Optional logger used by dispatch setup/execution logs. | `null` |
| `DispatchLoopCount` | Explicit worker-loop count. `null` means auto-select during `Activate()`. | `null` |
| `MaxDrainPerWakeMultiplier` | Multiplier used to compute per-wake drain budget. | `8` |
| `MinDrainPerWake` | Lower clamp for per-wake drain budget. | `64` |
| `MaxDrainPerWake` | Upper clamp for per-wake drain budget. | `2048` |
| `MinDispatchLoops` | Lower clamp for auto loop selection. | `1` |
| `MaxDispatchLoops` | Upper clamp for auto loop selection. | `64` |
| `MaxInternalQueueSize` | Reserved tuning property on options; per-connection queue bounds are enforced by `DispatchOptions`. | `100000` |

## Fluent configuration methods

| Method | Behavior |
|---|---|
| `WithLogging(ILogger logger)` | Attaches logger used by dispatch diagnostics. |
| `WithErrorHandling(Action<Exception, ushort> errorHandler)` | Registers global dispatch error callback. |
| `WithMiddleware(IPacketMiddleware<TPacket> middleware)` | Adds packet middleware to handler pipeline. Throws on `null`. |
| `WithBufferMiddleware(INetworkBufferMiddleware middleware)` | Adds pre-deserialization middleware. Throws on `null`. |
| `WithDispatchLoopCount(int? loopCount)` | Sets explicit worker-loop count (`1..64`) or `null` for auto mode. |
| `WithErrorHandlingMiddleware(bool continueOnError, Action<Exception, Type>? errorHandler = null)` | Configures packet middleware pipeline error behavior. |
| `WithHandler<TController>()` | Registers handlers by creating `TController` via parameterless ctor. |
| `WithHandler<TController>(TController instance)` | Registers handlers from an existing controller instance. |
| `WithHandler<TController>(Func<TController> factory)` | Registers handlers from controller factory output. |

## Handler registration requirements

- Controller type must be annotated with `[PacketController]`.
- Handler methods are discovered via packet attributes (for example `[PacketOpcode]`).
- Duplicate opcode registrations throw `InternalErrorException`.

## Loop selection behavior

Worker-loop count is resolved by `PacketDispatchChannel.Activate()`:

- if `DispatchLoopCount` is set: use that value
- otherwise: `Math.Clamp(Environment.ProcessorCount, MinDispatchLoops, MaxDispatchLoops)`

## Example

```csharp
PacketDispatchChannel dispatch = new(options =>
{
    options.WithLogging(logger)
           .WithErrorHandling((ex, opcode) =>
           {
               logger.Error($"dispatch-error opcode=0x{opcode:X4}", ex);
           })
           .WithDispatchLoopCount(null)
           .WithBufferMiddleware(new SessionEnvelopeGuard())
           .WithMiddleware(new PermissionMiddleware())
           .WithMiddleware(new RateLimitMiddleware())
           .WithMiddleware(new TimeoutMiddleware())
           .WithHandler(() => new AccountHandlers());
});
```

## Related APIs

- [Packet Dispatch](./packet-dispatch.md)
- [Dispatch Contracts](./dispatch-contracts.md)
- [Middleware Pipeline](../middleware/pipeline.md)
- [Dispatch Options](../options/dispatch-options.md)
