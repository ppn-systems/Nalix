# Session Resumption Protocol

Session resumption enables low-latency reconnection by bypassing the full X25519 handshake. It allows clients to re-establish a secure session using previously negotiated symmetric secrets and a valid session token.

## 1. Design & Rationale

Nalix utilizes a **Unified Signal Flow** (introduced in v1.2) to manage session state. By consolidating the legacy `SessionResume` and `SessionResumeAck` packets into a single `SESSION_SIGNAL` packet with a `Stage` state machine, the protocol reduces complexity and ensures atomic state transitions.

- **Atomic Resumption**: The server applies the restored session snapshot and returns the (possibly rotated) token in a single round-trip.
- **Stateless Re-entry**: UDP sessions can be resumed immediately as long as the 7-byte `SessionToken` and its associated `Secret` are valid.
- **Token Rotation**: The server may issue a new `SessionToken` in every successful response to prevent long-term token leakage.

---

## 2. Handshake vs. Resumption

```mermaid
sequenceDiagram
    participant C as Client (SDK)
    participant S as Server (Runtime)
    
    Note over C, S: Option A: Full Handshake (Standard)
    C->>S: CLIENT_HELLO (X25519 PubKey)
    S->>C: SERVER_HELLO
    C->>S: CLIENT_FINISH
    S->>C: SERVER_FINISH (New Token)
    
    Note over C, S: Option B: Session Resume (Fast)
    C->>S: SESSION_SIGNAL [Stage=REQUEST, Token=T1]
    S->>C: SESSION_SIGNAL [Stage=RESPONSE, Token=T2, Reason=NONE]
```

---

## 3. Protocol Specification

### Header & Payload
The `SESSION_SIGNAL` packet is a fixed-size frame of **23 bytes**.

| Offset | Field | Type | size | Description |
|---|---|---|---|---|
| 0 | `MagicNumber` | `int` | 4 | Fixed protocol magic. |
| 4 | `OpCode` | `ushort` | 2 | `0x0002` (SESSION_SIGNAL). |
| 6 | `Flags` | `byte` | 1 | Framing flags. |
| 7 | `Priority` | `byte` | 1 | Fixed at `0x03` (URGENT). |
| 8 | `Transport` | `byte` | 1 | `0x01` (TCP) or `0x02` (UDP). |
| 9 | `SequenceId` | `uint` | 4 | Correlation identifier. |
| 13 | `Stage` | `byte` | 1 | `0x01` (REQUEST), `0x02` (RESPONSE). |
| 14 | `SessionToken` | `Snowflake` | 7 | The 56-bit unique session identifier. |
| 21 | `Reason` | `ushort` | 2 | Result code (e.g., `0` for SUCCESS). |

---

## 4. Implementation Details

### Source Mapping
- **Packet Model**: `src/Nalix.Framework/DataFrames/SignalFrames/SessionResume.cs`
- **Control OpCode**: `ProtocolOpCode.SESSION_SIGNAL`
- **Server Logic**: `src/Nalix.Runtime/Handlers/SessionHandlers.cs`
- **Hub State**: `src/Nalix.Network/Connections/Connection.Hub.cs`

### Server Handling Logic
When a `SESSION_SIGNAL` request arrives at `SessionHandlers.Handle`:
1. The server extracts the `SessionToken` from the payload.
2. It attempts to resolve a valid `SessionEntry` via the `IConnectionHub`.
3. If found, the hub restores the connection's `Secret`, `SecurityLevel`, and `Attributes`.
4. A `RESPONSE` is generated with `ProtocolReason.NONE`.
5. If the token is invalid or expired, the server responds with an error code (e.g., `SESSION_EXPIRED`) and disconnects.

---

## 5. Security & Operations

- **Token Confidentiality**: While the token is not a replacement for the symmetric secret, it should be treated as sensitive material as it identifies an active session.
- **Rotation Policy**: Clients must update their local `TransportOptions.SessionToken` immediately upon receiving a successful `RESPONSE`.
- **Invalidation**: Sessions are invalidated upon explicit disconnect or after the configured hub TTL (Time-To-Live) expires.

---

## Related Documentation
- [Handshake Protocol (X25519)](./handshake.md)
- [Snowflake Identifiers](../framework/runtime/snowflake.md)
- [SDK Resume Extensions](../sdk/resume-extensions.md)
