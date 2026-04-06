# Handshake Protocol (X25519)

Nalix provides a default cryptographic handshake implementation based on the X25519 (Curve25519) elliptic curve Diffie-Hellman algorithm. This protocol ensures that session keys are derived securely without passing the final key over the network.

## Overview

The handshake process is anonymous and provides mutual authentication of the derived shared secret via transcript hashing and proofs.

### Stages

1.  **CLIENT_HELLO**: Client sends its ephemeral public key and a random nonce.
2.  **SERVER_HELLO**: Server responds with its own ephemeral public key, a nonce, and a proof (HMAC of the shared secret over the handshake transcript).
3.  **CLIENT_FINISH**: Client verifies the server proof, derives the session key, and sends its own proof to the server.
4.  **SERVER_FINISH**: Server verifies the client proof and sends a final confirmation proof.

Once the handshake is complete, both sides enable symmetric encryption (typically ChaCha20Poly1305) using the derived session key.

## Server-side Protocol: ProtocolX25519

The `ProtocolX25519` class implements the server-side logic. It is a sealed class inheriting from `Protocol`.

### Source Mapping

- `src/Nalix.Network/Protocols/Protocol.Handshake.cs`

### Usage

```csharp
var protocol = new ProtocolX25519();
protocol.Bind(connection);
```

When a connection is accepted, `ProtocolX25519` handles the incoming `Handshake` packets. Upon successful completion, it automatically unbinds itself and enables encryption on the connection.

## Client-side Extension: HandshakeAsync

The SDK provides an extension method on `TransportSession` to perform the handshake.

### Source Mapping

- `src/Nalix.SDK/Transport/Extensions/TcpSessionX25519Extensions.cs`

### Usage

```csharp
await session.ConnectAsync("localhost", 1234, ct);
await session.HandshakeAsync(ct);

// Session is now encrypted
await session.SendAsync(new MyPacket());
```

## Primitive: X25519

The core cryptographic operations are provided by the `X25519` static class.

### Source Mapping

- `src/Nalix.Framework/Security/Asymmetric/X25519.cs`

### Key APIs

- `GenerateKeyPair()`: Creates a new ephemeral key pair.
- `Agreement(privateKey, publicKey)`: Computes the shared secret using ECDH.

## Related Topics

- [Cryptography](./cryptography.md)
- [Protocol](../network/runtime/protocol.md)
- [TCP Session](../sdk/tcp-session.md)
