# Handshake Extensions

`HandshakeExtensions` provides the client-side X25519 handshake used to establish an encrypted `TransportSession`.
For reconnect flows, see [`ResumeExtensions`](./resume-extensions.md).

## Source mapping

- `src/Nalix.SDK/Transport/Extensions/HandshakeExtensions.cs`
- `src/Nalix.Codec/ProtocolFrames/Session/SessionInit.cs`
- `src/Nalix.Codec/ProtocolFrames/Session/SessionChallenge.cs`
- `src/Nalix.Codec/ProtocolFrames/Session/SessionProof.cs`
- `src/Nalix.Codec/ProtocolFrames/Session/SessionEstablished.cs`
- `src/Nalix.Codec/Security/HandshakeX25519.cs`

## Implementation Flow

```mermaid
sequenceDiagram
    participant App as Client App
    participant S as TransportSession
    participant Server as Server Runtime

    App->>S: HandshakeAsync(ct)
    S->>S: require IsConnected
    S->>S: generate X25519 key pair + client nonce
    S->>Server: SessionInit (PublicKey, Nonce)
    Server-->>S: SessionChallenge or ERROR
    S->>S: Validate SessionChallenge
    S->>S: require Options.ServerPublicKey
    S->>S: derive EE + static shared secrets
    S->>S: verify server proof
    S->>S: set Secret, Algorithm
    S->>Server: SessionProof (Proof)
    Server-->>S: SessionEstablished or ERROR
    S->>S: Validate SessionEstablished + proof
    S->>S: set EncryptionEnabled, SessionToken
```

## Role and Design

This helper performs the full client-side handshake sequence after a transport is connected.

- **Ephemeral key exchange**: Generates a fresh X25519 key pair for each handshake.
- **Proof verification**: Validates the server's response before deriving session material.
- **Session activation**: Updates `TransportOptions.Secret` and `TransportOptions.Algorithm` before sending `SessionProof`, then sets `TransportOptions.EncryptionEnabled` and `TransportOptions.SessionToken` after validating `SessionEstablished`.

## API Reference

| Method | Description |
| --- | --- |
| `HandshakeAsync` | Performs the client-side X25519 handshake on a connected `TransportSession`. |

## Basic usage

```csharp
// 1. Configure the expected server public key (Identity Pinning)
client.Options.ServerPublicKey = "your-server-public-key-hex";

// 2. Connect and perform authenticated handshake
await client.ConnectAsync();
await client.HandshakeAsync();
```

## Important notes

- Call this only after the session is connected.
- **Identity Pinning is Mandatory**: The client MUST provide the expected server public key via `TransportOptions.ServerPublicKey`. Anonymous handshakes are strictly forbidden to prevent MitM attacks.
- On success, the session switches to `CipherSuiteType.Chacha20Poly1305`.
- `HandshakeAsync(...)` uses `RequestAsync<SessionInit>(...)` and `RequestAsync<SessionProof>(...)` for the handshake exchange, filtering for the expected response frames.

## Related APIs

- [Session Extensions](./tcp-session-extensions.md)
- [Handshake Protocol](../security/handshake.md)
