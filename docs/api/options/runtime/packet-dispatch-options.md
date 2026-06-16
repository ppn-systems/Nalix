# Packet Dispatch Options

`PacketDispatchOptions<TPacket>` defines how `PacketDispatchChannel` resolves handlers, executes middleware, and sizes its worker-loop behavior.

## Source mapping

- `src/Nalix.Runtime/Dispatching/Options/PacketDispatchOptions.cs`
- `src/Nalix.Runtime/Dispatching/Options/PacketDispatchOptions.PublicMethods.cs`
- `src/Nalix.Runtime/Dispatching/Options/PacketDispatchOptions.Execution.cs`

## Type summary

- Generic constraint: `where TPacket : IPacket`
- Purpose: registration-time configuration for handler table and packet middleware.

## Public properties

| Property | Meaning | Default |
| --- | --- | ---: |
| `Drain` | Sub-options controlling dispatch loop drain behavior. | See below |
| `Drain.MaxDrainPerWakeMultiplier` | Multiplier used to compute per-wake drain budget. | `5` |
| `Drain.MinDrainPerWake` | Lower clamp for per-wake drain budget. | `64` |
| `Drain.MaxDrainPerWake` | Upper clamp for per-wake drain budget. | `2048` |
| `Drain.MinDispatchLoops` | Lower clamp for auto loop selection. | `1` |
| `Drain.MaxDispatchLoops` | Upper clamp for auto loop selection. | `64` |

## Fluent configuration methods

| Method | Behavior |
| --- | --- |
| `WithErrorHandling(Action<Exception, ushort> errorHandler)` | Registers global dispatch error callback. |
| `WithMiddleware(IPacketMiddleware<TPacket> middleware)` | Adds packet middleware to handler pipeline. Throws on `null`. |
| `WithDispatchLoopCount(int? loopCount)` | Sets explicit worker-loop count (`1..64`) or `null` for auto mode. |
| `WithErrorHandlingMiddleware(bool continueOnError, Action<Exception, Type>? errorHandler = null)` | Configures packet middleware pipeline error behavior. |
| `WithHandler<TController>()` | Registers handlers by creating `TController` via parameterless ctor. |
| `WithHandler<TController>(TController instance)` | Registers handlers from an existing controller instance. |
| `WithHandler<TController>(Func<TController> factory)` | Registers handlers from controller factory output. |

## Handler registration requirements

- Controller type must be annotated with `[PacketHandler]`.
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
    options.WithErrorHandling((ex, opcode) =>
           {
               // Custom error hook for handler exceptions
           })
           .WithDispatchLoopCount(null)
           .WithMiddleware(new PermissionMiddleware())
           .WithMiddleware(new RateLimitMiddleware())
           .WithMiddleware(new TimeoutMiddleware())
           .WithHandler(() => new AccountHandlers());
});
```

!!! info "Diagnostics"
    Runtime diagnostics are emitted via `DiagnosticListener` (`"Runtime"`, event source
    `Nalix.Runtime.DiagnosticsEvents.Source`). No per-instance logger attachment is needed.

## Related APIs

- [Packet Dispatch](../../runtime/routing/packet-dispatch.md)
- [Dispatch Contracts](../../runtime/routing/dispatch-contracts.md)
- [Middleware Pipeline](../../runtime/middleware/pipeline.md)
- [Dispatch Options](./dispatch-options.md)
