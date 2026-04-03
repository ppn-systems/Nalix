# Protocol String Extensions

`ProtocolStringExtensions` turns low-level protocol enums into short user-facing strings.

Use this page when you want readable text for UI, logs, or error messages without exposing raw enum names.

## Source mapping

- `src/Nalix.SDK/Extensions/ProtocolStringExtensions.cs`

## Main types

- `ProtocolStringExtensions`

## What it provides

The extension methods currently cover:

- `ProtocolAdvice.ToString()`
- `ProtocolReason.ToString()`

## Why this exists

The raw protocol enums are useful in code, but they are not always ideal for:

- client UI messages
- toast or dialog text
- logs that should stay readable for operators

These helpers give you a compact display string without making the calling code repeat the same mapping logic everywhere.

## Example

```csharp
string advice = ProtocolAdvice.BACKOFF_RETRY.ToString();
string reason = ProtocolReason.RATE_LIMITED.ToString();

Console.WriteLine($"{reason}: {advice}");
```

## Related APIs

- [SDK Overview](./index.md)
- [TCP Session Extensions](./tcp-session-extensions.md)
- [Session Diagnostics](./diagnostics.md)
