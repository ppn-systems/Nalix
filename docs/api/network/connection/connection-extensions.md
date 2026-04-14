# Connection Extensions

`ConnectionExtensions` provides directive send helpers on top of `IConnection`.

## Audit Summary

- Existing page was mostly correct but needed explicit source path correction and tighter API mapping.

## Missing Content Identified

- Exact extension signature and options record fields from current implementation.

## Improvement Rationale

This avoids drift between helper docs and runtime call sites.

## Source Mapping

- `src/Nalix.Runtime/Extensions/ConnectionExtensions.cs`

## Core API

```csharp
Task SendAsync(this IConnection connection,
    ControlType controlType,
    ProtocolReason reason,
    ProtocolAdvice action,
    ControlDirectiveOptions options = default)
```

Options payload:

- `Flags`
- `SequenceId`
- `Arg0`
- `Arg1`
- `Arg2`

## Why It Exists

It centralizes creation/serialization/sending of `Directive` frames so callers can send control-plane responses without manual frame composition.

## Practical Example

```csharp
await connection.SendAsync(
    controlType: ControlType.THROTTLE,
    reason: ProtocolReason.RATE_LIMITED,
    action: ProtocolAdvice.RETRY,
    options: new ControlDirectiveOptions(
        Flags: ControlFlags.NONE,
        SequenceId: 42));
```

## Related APIs

- [Connection](./connection.md)
- [Protocol](../protocol.md)
