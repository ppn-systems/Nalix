# Handshake

Nalix implements an authenticated X25519-based handshake using the built-in `Handshake` signal packet, `HandshakeHandlers` on the server, and `HandshakeExtensions` on the SDK side.

## Source Mapping

- `src/Nalix.Codec/DataFrames/SignalFrames/Handshake.cs`
- `src/Nalix.Codec/Security/HandshakeX25519.cs`
- `src/Nalix.Runtime/Handlers/HandshakeHandlers.cs`
- `src/Nalix.SDK/Transport/Extensions/HandshakeExtensions.cs`
- `src/Nalix.Codec/ProtocolFrames/KeyExchange.cs`
- `src/Nalix.Runtime/Handlers/KeyExchangeHandlers.cs`

## 1. Handshake Flow

The handshake consists of 4 stages managed by the `Handshake` packet and `HandshakeHandlers`.

| Stage | Description | Key Payload |
| --- | --- | --- |
| **CLIENT_HELLO** | Client initiates and sends its ephemeral public key. | `PublicKey`, `Nonce` |
| **SERVER_HELLO** | Server responds with its key, a challenge, identity proof, and transcript hash. | `PublicKey`, `Nonce`, `Proof`, `TranscriptHash` |
| **CLIENT_FINISH** | Client verifies server identity and sends its final proof. | `Proof`, `TranscriptHash` |
| **SERVER_FINISH** | Server confirms and assigns a session token (Snowflake). | `Proof`, `TranscriptHash`, `SessionToken` |

---

## 2. Trust on First Use (TOFU) Key Exchange

If the client's `TransportOptions.ServerPublicKey` configuration is empty or null, the client executes a Trust On First Use (TOFU) key exchange flow before initiating the X25519 handshake sequence:

1. **Request Stage (`KeyExchangeStage.REQUEST`)**: The client creates a `KeyExchange` packet (opcode `ProtocolOpCode.KEY_EXCHANGE` / `0x0003`) with stage `KeyExchangeStage.REQUEST` and sends it to the server.
2. **Server Response**: The server's `KeyExchangeHandlers` controller handles this packet. It verifies that the handshake has not already been established (otherwise rejects as state violation) and replies with a `KeyExchange` packet of stage `KeyExchangeStage.RESPONSE` containing the server's static public key (`HandshakeHandlers.ServerPublicKey`).
3. **Client Configuration Update**: The client receives the `KeyExchangeStage.RESPONSE`, validates the public key, updates `session.Options.ServerPublicKey` dynamically, updates this value in `ConfigurationManager`, and flushes the configuration to disk to remember it for future connections.
4. **Resuming Handshake**: With the server public key now available, the client proceeds with the standard X25519 handshake sequence (`SERVER_HELLO`, `CLIENT_FINISH`, etc.).

---

## 3. Cryptographic Construction

`HandshakeX25519` is the helper used to derive the master secret, transcript hash, proofs, and final session key used by the transport after a successful handshake.

### Hashing Strategy (`HandshakeX25519`)

The protocol derives its master secret from:

- ephemeral-ephemeral agreement
- static-ephemeral agreement against the pinned server key

| Purpose | Label | Components |
| --- | --- | --- |
| **Master Secret** | `nalix-handshake/master-secret` | `SharedSecret_EE`, `SharedSecret_SE` |
| **Server Proof** | `nalix-handshake/server-proof` | `MasterSecret`, `TranscriptHash` |
| **Client Proof** | `nalix-handshake/client-proof` | `MasterSecret`, `TranscriptHash` |
| **Server Finish** | `nalix-handshake/server-finish` | `MasterSecret`, `TranscriptHash` |
| **Session Key** | `nalix-handshake/session` | `MasterSecret`, `ClientNonce`, `ServerNonce`, `TranscriptHash` |

---

## 4. Server Implementation

The server-side state machine is implemented in `HandshakeHandlers`. It tracks negotiation state through `connection.Attributes`, specifically `ConnectionAttributes.HandshakeState` during the handshake and `ConnectionAttributes.HandshakeEstablished` once the handshake completes.

### Cryptographic Methods (`HandshakeX25519`)

- `ComputeMasterSecret(Bytes32, Bytes32)`: Combines ephemeral-ephemeral and static-ephemeral shared secrets into the master secret.
- `ComputeServerProof(Bytes32, Bytes32)`: Generates the proof for `SERVER_HELLO`.
- `ComputeClientProof(Bytes32, Bytes32)`: Generates the proof for `CLIENT_FINISH`.
- `ComputeServerFinishProof(Bytes32, Bytes32)`: Generates the final acknowledgement proof for `SERVER_FINISH`.
- `DeriveSessionKey(Bytes32, Bytes32, Bytes32, Bytes32)`: Derives the 32-byte session key from the shared secret, client nonce, server nonce, and transcript hash.
- `ComposeTranscriptBuffer(Bytes32, Bytes32, Bytes32, Bytes32)`: Composes the raw transcript buffer from public keys and nonces. Callers should wipe the returned buffer after hashing.
- `ComputeTranscriptHash(Bytes32, Bytes32, Bytes32, Bytes32)`: Computes the handshake transcript hash from public keys and nonces, securely clearing the temporary buffer.

### Handling Logic

Upon `CLIENT_FINISH` verification, the handler:

1. Derives the 32-byte session key.
2. Sets `connection.Secret` and `connection.Algorithm` (ChaCha20Poly1305).
3. Marks the connection as established through the built-in connection attribute key.
4. Persists resumable session state through `IConnectionHub.SessionService.SaveSessionAsync(connection)` when a hub is available.
5. Returns a `SessionToken` to the client in `SERVER_FINISH`.

---

## 5. Client SDK Usage

The `Nalix.SDK` provides an automated extension to perform the handshake after connection.

```csharp
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;

await session.ConnectAsync("127.0.0.1", 5000);

// Executes the 4-stage X25519 flow (performs TOFU key exchange if no public key is pinned)
await session.HandshakeAsync(cancellationToken);

// Session is now transparently encrypted
await session.SendAsync(new SecurePacket());
```

---

## 6. Security Notes

- **Identity Authentication**: By configuring `ServerPublicKey` on the client and a server certificate path on the server, the handshake performs a key agreement that lets the client pin the server identity. **Anonymous handshakes are strictly forbidden** to prevent MitM attacks.
- **Mandatory Identity**: The client must be configured with `TransportOptions.ServerPublicKey` to pin the server identity. On the server, the identity key is loaded from `certificate.private` in the configuration directory by default, or from a custom path supplied through hosting configuration.
- **Structural Validation**: All stages are strictly validated via `IPacketValidatable` to prevent malformed packets or stage confusion attacks before any cryptography is performed.
- **Pooled packets**: Both server replies are created through `PacketFactory<Handshake>.Acquire()` in `src/Nalix.Runtime/Handlers/HandshakeHandlers.cs`.
- **Transcript Integrity**: Any modification to keys or nonces during transit will cause a `TranscriptHash` mismatch, resulting in an immediate `ProtocolReason.CHECKSUM_FAILED` rejection.
- **Resume Token**: The initial handshake finish sets `SessionToken = connection.ID.ToUInt64()` in `src/Nalix.Runtime/Handlers/HandshakeHandlers.cs`. Treat the token as resumable session state, not as a cryptographic secret by itself.

---

## Related Topics

- [AEAD & Envelope Encryption](./aead-and-envelope.md)
- [X25519 Primitives](./cryptography.md)
- [Snowflake Identifiers](../framework/snowflake.md)
- [Session Resume](./session-resume.md)
