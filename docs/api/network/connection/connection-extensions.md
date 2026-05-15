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
public static async Task SendAsync(this IConnection connection,
    ControlType controlType,
    ProtocolReason reason,
    ProtocolAdvice action,
    ControlDirectiveOptions options = default)
```
 
Options payload:
 
- `Flags` — `ControlFlags` mask
- `SequenceId` — Correlation ID (0 if none)
- `Arg0` — 4-byte generic argument
- `Arg1` — 4-byte generic argument
- `Arg2` — 2-byte generic argument
 
## Why It Exists
 
It centralizes creation, initialization from the `PacketFactory<Directive>` pool, and sending of `Directive` frames so callers can send control-plane responses without manual frame composition or manual pool management.
 
## Practical Example
 
```csharp
using Nalix.Runtime.Extensions;
 
await connection.SendAsync(
    controlType: ControlType.UPDATE,
    reason: ProtocolReason.SUCCESS,
    action: ProtocolAdvice.NONE,
    options: new ControlDirectiveOptions(Flags: ControlFlags.NONE, SequenceId: 42));
```

## Related APIs

- [Connection](./connection.md)
- [Protocol](../protocol.md)
