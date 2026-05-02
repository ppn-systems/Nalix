# Protocol String Extensions

`ProtocolStringExtensions` converts low-level protocol and network enums into human-readable strings. These are designed for display in client UIs, toast notifications, and human-oriented logs.

## Source mapping

- `src/Nalix.SDK/Extensions/ProtocolStringExtensions.cs`

## Role and Design

While raw enum values are efficient for transmission and logic, they often contain underscores or tech-heavy names (e.g., `BACKOFF_RETRY`) that are non-ideal for end users. This module provides a centralized "localization-lite" layer for standard protocol outcomes.

- **Centralized Mapping**: Ensures the entire application uses consistent terminology for the same protocol states.
- **UI Ready**: Strings are trimmed of technical prefixes and use standard English sentence casing.
- **Zero Allocation switch**: Uses a high-performance C# switch expression for O(1) retrieval.

## API Reference

### Extended Enums

| Enum | Extension Method | Purpose |
| --- | --- | --- |
| `ProtocolAdvice` | `ToDisplayString()` | Translates the "What should I do?" hint from the server. |
| `ProtocolReason` | `ToDisplayString()` | Translates the "Why did this happen?" error code. |

## Mappings at a glance

!!! note "Incomplete table"
    The source contains 70+ `ProtocolReason` values and 8 `ProtocolAdvice` values. The table below shows a representative subset. See `ProtocolStringExtensions.cs` for the complete mapping.

| Enum Value | Display String |
| --- | --- |
| `ProtocolAdvice.RETRY` | "Please try again." |
| `ProtocolAdvice.BACKOFF_RETRY` | "Please wait and try again." |
| `ProtocolReason.RATE_LIMITED` | "Too many requests." |
| `ProtocolReason.TOKEN_EXPIRED` | "Session expired." |
| `ProtocolReason.THROTTLED` | "Request throttled." |

## Basic usage

```csharp
// Receive a control frame from the server
var control = await client.AwaitControlAsync(...);

// Convert tech codes to user info
string userReason = control.Reason.ToDisplayString(); // "Session expired."
string userAdvice = ProtocolAdvice.REAUTHENTICATE.ToDisplayString(); // "Sign in again required."

ShowToast($"{userReason} {userAdvice}");
```

## Related APIs

- [SDK Overview](./index.md)
- [Handshake Extensions](./handshake-extensions.md)
- [Control Type Enum](../abstractions/protocols/control-type.md)
