# Resume Extensions

`ResumeExtensions` provides the client-side reconnect flow for session resume.

## Source mapping

- `src/Nalix.SDK/Transport/Extensions/ResumeExtensions.cs`

## API Reference

| Method | Description |
|---|---|
| `ResumeSessionAsync` | Sends a `SessionResume` request and updates the client transport state from the server response. |
| `ConnectWithResumeAsync` | Connects, attempts session resume, and falls back to a fresh handshake when allowed. |

## Behavior

- If the session has a valid `SessionToken` and `Secret`, the SDK tries to resume the session first.
- On success, the SDK updates `TransportOptions.SessionToken` and `TransportOptions.EncryptionEnabled`.
- If resume fails and fallback is enabled, the SDK performs a fresh handshake to re-authenticate and re-key.
- If the server rotates the token, the client automatically adopts the new token returned in the response.

## Example

```csharp
// Connect and attempt to resume, falling back to handshake if needed
bool resumed = await session.ConnectWithResumeAsync();

if (resumed)
{
    Console.WriteLine("Session resumed successfully.");
}
else
{
    Console.WriteLine("Fresh handshake performed.");
}
```
