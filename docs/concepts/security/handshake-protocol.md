# Handshake Protocol

Nalix implements a high-security, zero-trust handshake protocol based on **X25519 (Curve25519)** Elliptic Curve Diffie-Hellman (ECDH). This protocol ensures that every session is encrypted with a unique session key that is never transmitted over the wire.

## Security Guarantees
- **Mutual Agreement**: Both client and server contribute to the final session key.
- **Perfect Forward Secrecy (PFS)**: Ephemeral keys are used for every session.
- [x] **Identity Verification**: Requires pinned server public keys to prevent Man-in-the-Middle (MitM) attacks. Anonymous handshakes are strictly forbidden; both the client and server must load valid identities from standardized storage.

!!! critical "Mandatory Identity"
    Every Nalix server must possess a `certificate.private` file. By default, the server looks for this file in the standardized configuration directory. You can override this path using `builder.ConfigureCertificate("path/to/identity")` during host construction.
    
    Clients must be configured with the server's `certificate.public` hash (or the full key) during the connection setup to enable public key pinning.

- [x] **Transcript Integrity**: All handshake messages are hashed into a transcript to prevent tampering or replay attacks.

## The Handshake Workflow

The diagram below illustrates the communication between the **Nalix SDK** and the **Nalix Server** handlers.

```mermaid
sequenceDiagram
    participant SDK as Client
    participant Srv as Server

    Note over SDK: Generate KeyPair & Nonce
    
    SDK->>Srv: CLIENT_HELLO<br/>[ClientPubKey, Nonce]
    
    Note over Srv: Resolve Handlers<br/>Generate SrvKeyPair<br/>Compute MasterSecret
    
    Srv->>SDK: SERVER_HELLO<br/>[SrvPubKey, Nonce, Proof]
    
    Note over SDK: Verify Proof<br/>Compute Secrets<br/>Derive SessionKey
    
    SDK->>Srv: CLIENT_FINISH<br/>[Proof, Hash]
    
    Note over Srv: Verify Proof<br/>Create Session<br/>Finalize Cipher
    
    Srv->>SDK: SERVER_FINISH<br/>[FinishProof, Token]
    
    Note over SDK: Verify Proof<br/>Enable AEAD Encryption
```

## Protocol Stages

### 1. CLIENT_HELLO
The SDK initiates by sending its ephemeral public key and a cryptographically secure random nonce. No sensitive data is sent here.

### 2. SERVER_HELLO
The server responds with its own ephemeral public key and a `Proof`. The proof is a HMAC computed over the handshake transcript using the derived master secret, proving the server possesses the corresponding private key without revealing it.

### 3. CLIENT_FINISH
The SDK validates the server's proof. If valid, it computes its own `ClientProof` and sends it back. This confirms to the server that the client has successfully derived the same shared secret.

### 4. SERVER_FINISH
Final confirmation. The server sends a `SessionToken` which the client can use later for [Session Resumption](./session-resumption.md). The connection is now fully encrypted using the derived session key.

## Implementation Details
- **Encryption**: Once established, all subsequent packets use **ChaCha20-Poly1305** AEAD.
- **Key Rotation**: Session keys are only valid for the duration of the physical connection or until a session resume occurs.

## Related Topics
- [Session Resumption](./session-resumption.md)
- [Encryption Model](./encryption-model.md)
- [Zero-Allocation Design](../internals/zero-allocation.md)
