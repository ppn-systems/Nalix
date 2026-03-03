# Session Resume

Nalix supports a dedicated session-resume packet flow for reconnecting clients that already possess a valid session token and symmetric secret.

## Source mapping

- `src/Nalix.Framework/DataFrames/SignalFrames/SessionResume.cs`
- `src/Nalix.Framework/DataFrames/SignalFrames/SessionResumeAck.cs`
- `src/Nalix.Runtime/Handlers/SessionHandlers.cs`
- `src/Nalix.Runtime/Sessions/MemorySessionManager.cs`

## Flow

1. The client sends `SessionResume` with its existing `SessionToken`.
2. The server looks up the snapshot through `ISessionManager`.
3. On success, the server restores `Secret`, `Algorithm`, `Level`, and allowed attributes onto the new connection.
4. The server returns `SessionResumeAck` with the active token to keep using.
5. If token rotation is enabled, the response includes a replacement token and the old one is invalidated.

## Packet shape

`SessionResume`

- `SessionToken`
- `Timestamp`

`SessionResumeAck`

- `Success`
- `Reason`
- `SessionToken`
- `Algorithm`
- `Level`
- `Timestamp`

## Notes

- This is the v1 internal resume flow, not a final hardened replay-proof design.
- The server returns the token the client should keep using, so reconnect logic can update local transport state immediately.
- UDP lookup paths can resolve the new token through the active session manager instead of relying only on the connection id.
