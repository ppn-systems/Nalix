# 🔐 Nalix AI Skill — Security & Identity Internals

This skill covers the security protocols used in Nalix, including transport encryption, identity verification, and protection against network-level attacks.

---

## 🛡️ Key Security Pillars

### 1. Identity & Key Exchange (X25519)
Nalix uses Elliptic Curve Diffie-Hellman (ECDH) via Curve25519 for secure key exchange.

- **Static-Ephemeral:** The server uses a static key (identity pinning), while clients use ephemeral keys for perfect forward secrecy.
- **Agreement:** `SharedSecret = X25519.Agreement(privateKey, publicKey)`.

### 2. AEAD Encryption (ChaCha20-Poly1305)
Standard transport encryption uses ChaCha20 for stream encryption and Poly1305 for authentication.

- **Nonce Management:** Nonces must be unique and never reused for the same key.
- **Envelope:** Every encrypted packet is wrapped in an AEAD envelope.

### 3. Anti-Replay (SlidingWindow)
To prevent replay attacks, Nalix uses a sequence-number-based sliding window.

- **Component:** `SlidingWindow`.
- **Logic:** Tracks the highest seen sequence number and maintains a bitmask of recently seen numbers.
- **Rejection:** Packets with duplicate sequence numbers or numbers outside the window (too old) are dropped immediately.

---

## 🛤️ Security Pipeline

1. **Accept:** Raw socket connection.
2. **Handshake:** ECDH exchange -> Establish symmetric keys.
3. **Transport:**
    - **Inbound:** Decrypt -> Verify Poly1305 -> Check `SlidingWindow` -> Dispatch.
    - **Outbound:** Increment Nonce -> Encrypt -> Send.

---

## ⚡ Performance Considerations

- **SIMD Acceleration:** Nalix leverages SIMD (AVX2/Vector256) where possible for ChaCha20.
- **Zero-Allocation:** Encryption/Decryption happens in-place on Spans when possible.
- **Identity Pinning:** Pre-load server public keys on the client to prevent Man-in-the-Middle (MitM) attacks.

---

## 🧪 Advanced Features

### Zero-RTT Session Resumption
Clients can resume sessions without a full handshake by using a `SessionToken`.

- **Mechanism:** The server caches the symmetric key indexed by the token.
- **Validation:** Client sends `ControlType.RESUME` + `Token`.
- **Restoration:** If the token is valid and not expired, the encrypted tunnel is restored instantly.

---

## 🛡️ Common Pitfalls

- **Nonce Reuse:** Reusing a nonce with the same key breaks the encryption.
- **Cleartext Opcodes:** Ensure `[PacketEncryption(false)]` is only used for non-sensitive system packets (e.g., Ping).
- **Log Exposure:** Never log raw decrypted payloads or private keys.
