# ChaCha20Poly1305 — AEAD (Authenticated Encryption) for .NET

The **ChaCha20Poly1305** API provides fast, modern, and allocation-minimal [AEAD](https://en.wikipedia.org/wiki/Authenticated_encryption) encryption for .NET.  
It follows [RFC 8439](https://tools.ietf.org/html/rfc8439), combining ChaCha20 (stream cipher) and Poly1305 (MAC/tag) for robust authenticated encryption.

- **Namespace:** `Nalix.Shared.Security.Aead`
- **Class:** `ChaCha20Poly1305` (static)
- **Recommended for:** Secure messaging, WireGuard, TLS, disk encryption, and all modern crypto-protocols

---

## Features

- **AEAD:** Encrypts and authenticates (tag) your data
- 32-byte (256-bit) keys (required)
- 12-byte (96-bit) nonce (MUST be unique per message & key!)
- Tag: 16 bytes (Poly1305, strong constant-time MAC)
- Supports Additional Authenticated Data (AAD)
- High-performance, allocation-free Span APIs plus byte[] (convenience) overloads

---

## Typical Usage

### Encrypt (detached tag or combined ct||tag)

```csharp
using Nalix.Shared.Security.Aead;

byte[] key    = ...; // 32 bytes (CSPRNG)
byte[] nonce  = ...; // 12 bytes (unique per message!)
byte[] plain  = ...;
byte[] aad    = ...; // optional, can be empty

// One-shot (returns ciphertext||tag)
byte[] cipherTag = ChaCha20Poly1305.Encrypt(key, nonce, plain, aad);

// Detached
byte[] ciphertext = new byte[plain.Length];
Span<byte> tag = stackalloc byte[16];
ChaCha20Poly1305.Encrypt(key, nonce, plain, aad, ciphertext, tag);
```

### Decrypt & Authenticate

```csharp
// Combined (ct||tag)
byte[] restored = ChaCha20Poly1305.Decrypt(key, nonce, cipherTag, aad);
// Throws if authentication fails!

// Detached
byte[] plaintext = new byte[ciphertext.Length];
int n = ChaCha20Poly1305.Decrypt(key, nonce, ciphertext, aad, tag, plaintext);
if (n < 0) throw new Exception("Authentication failed");
```

---

## API Overview

| Method (Detached)                                            | Description                                          |
|--------------------------------------------------------------|------------------------------------------------------|
| `Encrypt(key, nonce, plain, aad, out ct, out tag)`           | AEAD encrypt, tag separate.                          |
| `Decrypt(key, nonce, ct, aad, tag, out plain)`               | AEAD decrypt+verify tag; returns n >= 0 if ok.       |

| Method (One-shot)                                                            | Returns    | Description                                         |
|------------------------------------------------------------------------------|------------|-----------------------------------------------------|
| `Encrypt(byte[] key, byte[] nonce, byte[] plain, byte[]? aad = null)`        | `byte[]`   | Returns `ciphertext \|\| tag`                       |
| `Decrypt(byte[] key, byte[] nonce, byte[] cipherTag, byte[]? aad = null)`    | `byte[]`   | Plaintext, or throws if tag check fails             |

**All tag/auth must be validated! Never use plaintext if decrypt returns < 0 or throws.**

---

## Parameters

| Name        | Size      | Description                                      |
|-------------|-----------|--------------------------------------------------|
| `key`       | 32 B      | *Must* be random, auto-generated per session     |
| `nonce`     | 12 B      | Must be unique per key/message (never re-use!)   |
| `aad`       | any       | Additional Authenticated Data, optional          |
| `tag`       | 16 B      | Poly1305 MAC output                              |
| `plain`     | any       | Raw data to encrypt                              |
| `ciphertext`| any       | Output buffer                                    |

---

## Security & Best Practices

- **Never reuse `(key, nonce)` pairs**. Reuse instantly destroys security!
- Always verify tag before processing decrypted output.
- Use CSPRNG for both keys and nonces (do *not* use timestamps/counters unless guaranteed unique).
- When handling secret keys/tags, zero them as soon as possible after use.

---

## Error Handling

- Throws `ArgumentException` if you pass wrong key/nonce/tag length or buffers too small.
- Decrypt returns -1 or throws if authentication fails.
- Never returns null for plain/cipher.

---

## Technical Details

- Poly1305 one-time MAC key = first 32 bytes of ChaCha20 (nonce, counter 0) keystream.
- Data uses ChaCha20 (nonce, counter 1+).
- Transcript = AAD || pad16 || ciphertext || pad16 || lengthAAD || lengthCT (see [RFC 8439, Section 2.8.1](https://tools.ietf.org/html/rfc8439#section-2.8.1))

---

## Example: AEAD Roundtrip

```csharp
// Encrypt
byte[] key = ...;     // 32 bytes, secure random
byte[] nonce = ...;   // 12 bytes, unique!
byte[] plain = ...;
byte[] aad = ...;     // can be empty

byte[] ctTag = ChaCha20Poly1305.Encrypt(key, nonce, plain, aad);

// Decrypt (with AAD)
byte[] clear = ChaCha20Poly1305.Decrypt(key, nonce, ctTag, aad);
// Throws or fails if auth/tag check fails
```

---

## Reference

- [RFC 8439 - ChaCha20 and Poly1305 for IETF Protocols](https://tools.ietf.org/html/rfc8439)
- Used by TLS 1.3, WireGuard VPN, OpenSSH, modern secure messaging

---

## License

Licensed under the Apache License, Version 2.0.
