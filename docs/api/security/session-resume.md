# Session Resume

Nalix supports a dedicated session-resume packet flow for reconnecting clients that already possess a valid session token and symmetric secret. This flow uses a unified `SESSION_SIGNAL` packet to manage the state machine.

## Source mapping

- `src/Nalix.Framework/DataFrames/SignalFrames/SessionResume.cs`
- `src/Nalix.Runtime/Handlers/SessionHandlers.cs`
- `src/Nalix.Runtime/Sessions/MemorySessionManager.cs`

## Flow

1. The client sends a `SessionResume` packet with `Stage = REQUEST` and its existing `SessionToken`.
2. The server looks up the snapshot through `ISessionManager`.
3. On success:
    - The server restores `Secret`, `Algorithm`, `Level`, and allowed attributes onto the new connection.
    - The server returns a `SessionResume` packet with `Stage = RESPONSE`, `Reason = NONE`, and the (possibly rotated) `SessionToken`.
4. On failure:
    - The server returns a `SessionResume` packet with `Stage = RESPONSE`, an appropriate `Reason` (e.g., `SESSION_EXPIRED`), and disconnects.

## Packet shape (`SESSION_SIGNAL` 0x0002)

`SessionResume` is a fixed-size packet (17 bytes):

| Field | Type | Size | Description |
|---|---|---|---|
| `OpCode` | `ushort` | 2 | Fixed at `0x0002`. |
| `Protocol` | `byte` | 1 | Transport protocol (TCP/UDP). |
| `Priority` | `byte` | 1 | Packet priority (usually `URGENT`). |
| `Stage` | `byte` | 1 | `0x01` for REQUEST, `0x02` for RESPONSE. |
| `SessionToken` | `Snowflake` | 8 | The session identifier. |
| `Reason` | `ProtocolReason` | 4 | Status code for the operation. |

## Notes

- This is the v1 internal resume flow, not a final hardened replay-proof design.
- The server returns the token the client should keep using, so reconnect logic can update local transport state immediately.
- UDP lookup paths can resolve the new token through the active session manager instead of relying only on the connection id.
