# 🔐 Nalix AI Skill — SDK Handshake & Security Orchestration

This skill covers the client-side implementation of the Nalix security handshake and session persistence mechanisms.

---

## 🏗️ The Handshake Sequence

The handshake is the process of establishing a secure, encrypted channel between the client and the server.

### Steps (`HandshakeExtensions`):
1.  **Client Hello:** Client sends a `Handshake` packet with its public X25519 key.
2.  **Server Hello:** Server responds with its public key and a random salt.
3.  **Key Exchange:** Both sides compute the shared secret and initialize their `ChaCha20Poly1305` ciphers.
4.  **Verification:** A MAC (Message Authentication Code) check is performed to ensure the keys were exchanged correctly.

---

## 📜 Session Resumption (Zero-RTT)

To avoid a full handshake on every reconnect, the SDK supports `SessionResume`.

- **`SessionToken`**: After a successful handshake, the server issues a token.
- **Persistence**: The SDK can persist this token to local storage (e.g., `client.ini` or a secure database).
- **Resumption**: On reconnect, the client sends the token in a `SessionResume` packet. If valid, the server restores the previous security context immediately.

---

## ⚡ Cipher Management

- **Rotating Keys:** (Optional) The SDK can request a key rotation after a certain amount of data is transmitted to maintain perfect forward secrecy.
- **Sequence Tracking:** The SDK maintains strict inbound and outbound sequence numbers. If a packet arrives out of the sliding window, it is dropped as a replay attempt.

---

## 🛤️ Security Best Practices

- **Identity Pinning:** Store the server's public key on the first successful connection and verify it on subsequent connections to prevent Man-in-the-Middle (MitM) attacks.
- **Token Protection:** Treat the `SessionToken` as a sensitive password. Never log it in plain text.
- **Hardware Acceleration:** The SDK automatically uses SIMD instructions for encryption if supported by the client CPU.

---

## 🛡️ Common Pitfalls

- **Handshake Timeout:** If the network is extremely laggy, the handshake might timeout. The SDK will automatically retry according to the `TransportOptions`.
- **Clock Skew:** Extreme clock skew between client and server can sometimes cause session tokens to be rejected if they have a short TTL.
- **Corrupted Persistence:** If the locally stored session token is corrupted, the resumption will fail, and the SDK must fall back to a full handshake.
