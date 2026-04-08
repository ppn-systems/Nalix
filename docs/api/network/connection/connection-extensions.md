# Connection Extensions

`ConnectionExtensions` adds convenience helpers on top of `IConnection`.

## Source mapping

- `src/Nalix.Runtime/Extensions/ConnectionExtensions.cs`

## What it does

The main public helper is a directive-oriented `SendAsync(...)` extension that sends control messages over a connection without forcing callers to build the underlying directive payload manually.

The optional `ConnectionExtensions.ControlDirectiveOptions` record groups directive flags, correlation, and payload arguments into one value.

## Basic usage

```csharp
await connection.SendAsync(
    controlType: ControlType.THROTTLE,
    reason: ProtocolReason.RATE_LIMITED,
    action: ProtocolAdvice.RETRY,
    options: new ConnectionExtensions.ControlDirectiveOptions(
        Flags: ControlFlags.NONE,
        SequenceId: 42));
```

## Why it is useful

Use this helper when the server wants to:

- send throttling or redirect instructions
- send control-plane responses with a reason and advice
- attach correlation IDs to protocol directives
- provide optional directive arguments without manually constructing `Directive`

## Related APIs

- [Connection](./connection.md)
- [Protocol](../protocol.md)
