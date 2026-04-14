# Handshake Protocol (X25519)

Nalix implements a high-performance anonymous handshake protocol based on **X25519** (Curve25519) and **Keccak-256**. This protocol establishes a secure, encrypted session key while ensuring transcript integrity through mutual proof verification.

## Source Mapping

- `src/Nalix.Framework/DataFrames/SignalFrames/Handshake.cs`
- `src/Nalix.Framework/Security/HandshakeX25519.cs`
- `src/Nalix.Runtime/Handlers/HandshakeHandlers.cs`
- `src/Nalix.SDK/Transport/Extensions/HandshakeExtensions.cs`

## 1. Handshake Flow

The handshake consists of 4 stages managed by the `Handshake` packet and `HandshakeHandlers`.

| Stage | Description | Key Payload |
|---|---|---|
| **CLIENT_HELLO** | Client initiates and sends its ephemeral public key. | `PublicKey`, `Nonce` |
| **SERVER_HELLO** | Server responds with its key, a challenge, and a proof. | `PublicKey`, `Nonce`, `Proof`, `TranscriptHash` |
| **CLIENT_FINISH** | Client verifies server proof and sends its final proof. | `Proof`, `TranscriptHash` |
| **SERVER_FINISH** | Server confirms and assigns a session token (Snowflake). | `Proof`, `TranscriptHash`, `SessionToken` |

---

## 2. Cryptographic Construction

Nalix uses a labeled digest construction to derive proofs and session keys. This prevents cross-protocol attacks and ensures that the handshake state is tied to the specific "nalix-handshake" domain.

### Hashing Strategy (`HandshakeX25519`)
All digests are computed using **Keccak-256** over length-prefixed segments:
`Hash(LabelLength + Label + Segment0Length + Segment0 + ...)`

| Purpose | Label | Components |
|---|---|---|
| **Server Proof** | `nalix-handshake/server-proof` | `SharedSecret`, `TranscriptHash` |
| **Client Proof** | `nalix-handshake/client-proof` | `SharedSecret`, `TranscriptHash` |
| **Server Finish** | `nalix-handshake/server-finish` | `SharedSecret`, `TranscriptHash` |
| **Session Key** | `nalix-handshake/session` | `SharedSecret`, `ClientNonce`, `ServerNonce`, `TranscriptHash` |

---

## 3. Server Implementation

The server-side state machine is implemented in `HandshakeHandlers`. It tracks the handshake state using the connection's `Attributes` during the negotiation phase.

### Cryptographic Methods (`HandshakeX25519`)
- `ComputeServerProof`: Generates the proof for `SERVER_HELLO`.
- `ComputeClientProof`: Generates the proof for `CLIENT_FINISH`.
- `ComputeServerFinishProof`: Generates the final acknowledgement proof for `SERVER_FINISH`.
- `DeriveSessionKey`: Derives the 32-byte session key from the shared secret and transcript.

### Handling Logic
Upon `CLIENT_FINISH` verification, the handler:
1. Derives the 32-byte session key.
2. Sets `connection.Secret` and `connection.Algorithm` (ChaCha20Poly1305).
3. Marks the connection as established via `connection.Attributes["nalix.handshake.established"]`.
4. Creates a resumable session snapshot through `ISessionStore` and persists it through the network session store.
5. Returns a `SessionToken` to the client in `SERVER_FINISH`.
6. Clears all ephemeral sensitive data (shared secrets, private keys) from memory.

---

## 4. Client SDK Usage

The `Nalix.SDK` provides an automated extension to perform the handshake after connection.

```csharp
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;

await session.ConnectAsync("127.0.0.1", 5000);

// Executes the 4-stage X25519 flow
await session.HandshakeAsync(cancellationToken);

// Session is now transparently encrypted
await session.SendAsync(new SecurePacket());
```

---

## 5. Security Notes

- **Zero-Allocation**: Handshake packets are pooled via `PacketBase`.
- **Memory Safety**: Private keys and shared secrets are passed as `ReadOnlySpan<byte>` and zeroed out explicitly using `MemorySecurity.ZeroMemory` after use.
- **Transcript Integrity**: Any modification to keys or nonces during transit will cause a `TranscriptHash` mismatch, resulting in an immediate `ProtocolReason.CHECKSUM_FAILED` rejection.
- **Resume Token**: The session token now comes from the session store and can be rotated on resume. Treat the token as resumable session state, not as a cryptographic secret by itself.

---

## Related Topics
- [AEAD & Envelope Encryption](./aead-and-envelope.md)
- [X25519 Primitives](./cryptography.md)
- [Snowflake Identifiers](../framework/runtime/snowflake.md)
- [Session Resume](./session-resume.md)
