# Resume Extensions

`ResumeExtensions` provides the client-side reconnect flow for session resume.

## Source mapping

- `src/Nalix.SDK/Transport/Extensions/ResumeExtensions.cs`

## API Reference

| Method | Description |
|---|---|
| `TryResumeAsync` | Sends `SessionResume` and updates the client transport state from the server response. |
| `ConnectAndResumeOrHandshakeAsync` | Connects, attempts resume, and falls back to handshake when allowed. |

## Behavior

- If the session has a valid `SessionToken` and `Secret`, the SDK tries resume first.
- On success, the SDK updates `TransportOptions.SessionToken`, `TransportOptions.Algorithm`, and `TransportOptions.EncryptionEnabled`.
- If resume fails and fallback is enabled, the SDK performs a fresh handshake.
- If the server rotates the token, the client keeps the new token returned in `SessionResumeAck`.

## Example

```csharp
await session.ConnectAndResumeOrHandshakeAsync();
```
