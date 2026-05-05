# Encryption Model

Nalix prioritizes security by enforcing modern, industry-standard cryptographic algorithms for all data transmission. The framework handles the complexities of AEAD (Authenticated Encryption with Associated Data), nonce management, and cipher state synchronization out of the box.

## Source Mapping

- `src/Nalix.Codec/Security/EnvelopeCipher.cs`
- `src/Nalix.Codec/Security/Engine/AeadEngine.cs`
- `src/Nalix.Codec/Security/Engine/SymmetricEngine.cs`
- `src/Nalix.Abstractions/Security/CipherSuiteType.cs`

## Primary Cipher: ChaCha20-Poly1305

By default, Nalix uses **ChaCha20-Poly1305**. This cipher suite was selected for several key reasons:

- **Performance**: Extremely fast on modern CPUs, even those without specialized hardware acceleration (unlike AES-GCM).
- **Security**: Provides high security margins and is resistant to many common side-channel attacks.
- **AEAD Support**: Automatically provides integrity checking—if a single bit of the packet is tampered with over the wire, the decryption will fail.

### Optional Plaintext Mode

The codec layer exposes a `CipherSuiteType.None` case in the current source tree. Treat this as an explicit plaintext/no-envelope mode for controlled environments only.

## Nonce Management

Correct nonce management is critical to the security of any stream cipher. Nalix manages nonce material internally inside the envelope and frame layers:

- **Fresh nonce generation**: `src/Nalix.Codec/Security/EnvelopeCipher.cs` generates a fresh random nonce per envelope encryption call.
- **Envelope sequencing**: Sequence metadata is carried in the envelope header and used by the underlying crypto engines according to the selected suite.

1. **Transport Level**: Packet framing and envelope metadata remain structured so the transport can process the message correctly.

2. **Payload Encryption**: The packet payload is encrypted using the session key derived during the [Handshake](./handshake-protocol.md).

3. **Integrity Tag**: AEAD suites append a **16-byte** authentication tag as part of the envelope format.

## Selective Encryption

Nalix allows developers to decide which packets require encryption using attributes. This is useful for performance optimization (e.g., non-sensitive movement updates in a game).

```csharp
using Nalix.Abstractions.Networking.Packets;

[PacketEncryption(false)] 
public class HeartbeatPacket : PacketBase<HeartbeatPacket> { ... }

[PacketEncryption(true)] 
public class PrivateMessage : PacketBase<PrivateMessage> { ... }
```

### Dynamic Overrides

In addition to static attributes, the **Nalix SDK** allows for per-call encryption control using the `encrypt` parameter in `SendAsync` methods.

```csharp
// Override the default/attribute policy for this specific call
await session.SendAsync(myPacket, encrypt: false);
```

!!! warning "Security First"
    By default, established secure sessions are expected to send encrypted traffic.  
    Disabling encryption (via attribute or parameter) should only be done for high-frequency, non-sensitive data where the overhead of AEAD is a bottleneck.

## Mathematical Correctness

The Nalix Framework's cryptographic implementations are engineered for both maximum performance and absolute correctness. To ensure safety, our primitives undergo rigorous **Interoperability Testing** against the industry-standard [BouncyCastle](https://www.bouncycastle.org/) library.

Our [Verified Cryptography Suite](../../guides/tools/index.md) confirms parity for:
- **Poly1305**: RFC 8439 compliant 130-bit modular arithmetic.
- **X25519**: RFC 7748 compliant Montgomery ladder and bit clamping.
- **AEAD Handlers**: Correct transcript construction for associated data (AAD) and padding.

This guarantees that data encrypted by Nalix is fully decryptable by any standard-compliant library, and that the underlying mathematics are free from common field-reduction errors.

## Related Topics

- [Handshake Protocol](./handshake-protocol.md)
- [Session Resumption](./session-resumption.md)
- [Zero-Allocation Hot Path](../internals/zero-allocation.md)
