# Encryption Model

Nalix prioritizes security by enforcing modern, industry-standard cryptographic algorithms for all data transmission. The framework handles the complexities of AEAD (Authenticated Encryption with Associated Data), nonce management, and cipher state synchronization out of the box.

## Primary Cipher: ChaCha20-Poly1305

By default, Nalix uses **ChaCha20-Poly1305**. This cipher suite was selected for several key reasons:

- **Performance**: Extremely fast on modern CPUs, even those without specialized hardware acceleration (unlike AES-GCM).
- **Security**: Provides high security margins and is resistant to many common side-channel attacks.
- **AEAD Support**: Automatically provides integrity checking—if a single bit of the packet is tampered with over the wire, the decryption will fail.

## Nonce Management

Correct nonce management is critical to the security of any stream cipher. Nalix manages nonces internally to prevent reuse (nonce misuse):

- **Predictable Increment**: Nonces are synchronized between the client and server and incremented with every packet.
- **Sequence Protection**: The `SequenceId` of the packet is often integrated into the nonce derivation to ensure that even if packets arrive out of order (in UDP), the correct decryption context can be restored.

1.**Transport Level**: The frame header (10 bytes) is typically sent in the clear (or with light obfuscation) to allow the [Packet System](../fundamentals/packet-system.md) to route the message.

2.**Payload Encryption**: The entire payload of the packet is encrypted using the session key derived during the [Handshake](./handshake-protocol.md).

3.**Integrity Tag**: A **16-byte** authentication tag is appended to the encrypted payload as part of the AEAD process.

## Selective Encryption

Nalix allows developers to decide which packets require encryption using attributes. This is useful for performance optimization (e.g., non-sensitive movement updates in a game).

```csharp
using Nalix.Framework.Security.Attributes;
using Nalix.Framework.DataFrames;

[PacketEncryption(false)] 
public class HeartbeatPacket : PacketBase<HeartbeatPacket> { ... }

[PacketEncryption(true)] 
public class PrivateMessage : PacketBase<PrivateMessage> { ... }
```

!!! warning "Security First"
    By default, Nalix assumes all packets should be encrypted.  
    Disabling encryption should only be done for high-frequency, non-sensitive data where the overhead of AEAD is a bottleneck.

## Related Topics

- [Handshake Protocol](./handshake-protocol.md)
- [Session Resumption](./session-resumption.md)
- [Zero-Allocation Hot Path](../internals/zero-allocation.md)
