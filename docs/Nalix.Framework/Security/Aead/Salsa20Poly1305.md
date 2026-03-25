# Salsa20Poly1305 — AEAD (Authenticated Encryption) for .NET

The **Salsa20Poly1305** API provides fast, allocation-efficient authenticated encryption combining Salsa20 (stream cipher) and Poly1305 (MAC/tag) according to the [secretbox construction](https://cr.yp.to/snuffle.html).  
Ensures both confidentiality (encryption) and integrity (authentication/tag) for your data — *never decrypt invalid/corrupted/crafted messages again*.

- **Namespace:** `Nalix.Shared.Security.Aead`
- **Class:** `Salsa20Poly1305` (static)
- **Based on:** [Cryptography in NaCl](https://nacl.cr.yp.to/), AEAD design, [Poly1305](https://cr.yp.to/mac.html)

---

## Features

- **AEAD:** Encrypts & authenticates data (tag+ciphertext)
- Key sizes: 16 or 32 bytes (Salsa20/128 or Salsa20/256)
- Nonce: 8 bytes (MUST be unique per key!)
- Tag: 16 bytes (Poly1305; prevents tampering, forgeries, accidental replays)
- Span-first, allocation-free APIs — or one-shot byte[] wrappers for convenience
- Support for additional authenticated data (AAD) — e.g., headers, metadata

---

## Typical Usage

### Encrypt (`ciphertext || tag`)

```csharp
using Nalix.Shared.Security.Aead;

byte[] key    = ...; // 16 or 32 bytes (CSPRNG)
byte[] nonce  = ...; // 8 bytes (unique per key, per message!)
byte[] plain  = ...;
byte[] aad    = ...; // can be empty

// One-shot usage:
byte[] cipherTag = Salsa20Poly1305.Encrypt(key, nonce, plain, aad); // returns CT||TAG

// Or "detached" (separate tag and ciphertext):
byte[] ciphertext = new byte[plain.Length];
Span<byte> tag    = stackalloc byte[16];
Salsa20Poly1305.Encrypt(key, nonce, plain, aad, ciphertext, tag);
```

### Decrypt & Authenticate

```csharp
byte[] cipherTag = ...; // ciphertext||tag
byte[] plain = Salsa20Poly1305.Decrypt(key, nonce, cipherTag, aad);
// Throws if authentication/tag check fails!

// Or "detached" (separate tag and ciphertext):
int ctLen = ...;
byte[] plaintext = new byte[ctLen];
int bytesRead = Salsa20Poly1305.Decrypt(key, nonce, ct, aad, tag, plaintext);
if (bytesRead < 0) throw new Exception("Authentication failed");
```

---

## API Overview

| Method (Detached)                                    | Description                 |
|------------------------------------------------------|-----------------------------|
| `Encrypt(key, nonce, plain, aad, out ct, out tag)`   | Authenticated encryption    |
| `Decrypt(key, nonce, ct, aad, tag, out plain)`       | Decrypt+authenticate        |

| Method (One-shot)                                                             | Returns  | Description                                      |
|-------------------------------------------------------------------------------|----------|--------------------------------------------------|
| `Encrypt(byte[] key, byte[] nonce, byte[] plain, byte[]? aad = null)`         | `byte[]` | Returns `ciphertext \|\| tag`                    |
| `Decrypt(byte[] key, byte[] nonce, byte[] cipherWithTag, byte[]? aad = null)` | `byte[]` | Returns plaintext or throws on auth failure      |

---

## Parameter Reference

| Name           | Size      | Description                                                    |
|----------------|-----------|----------------------------------------------------------------|
| `key`          | 16/32 B   | Secret key; must be random!                                    |
| `nonce`        | 8 B       | Nonce (unique per key *per encryption*)                        |
| `aad`          | any       | Additional authenticated data (optional)                       |
| `tag`          | 16 B      | Poly1305 MAC; never trust or output messages without verifying!|
| `plaintext`    | any       | Original message/data                                          |
| `ciphertext`   | any       | Output of encryption                                           |

---

## Security & Best Practices

- **Nonce reuse is fatal:** Every (key, nonce) pair can only be used ONCE for encryption!  
  Reusing a nonce for two messages *will* break secrecy and authentication.
- **ALWAYS verify tag before accepting any decrypted plaintext.** On failure: never use the plaintext!
- Consider using CSPRNG output (not counters, not timestamps) for nonces
- When using the span API, securely erase (`ZeroMemory`) all keys/tags as soon as possible.
- For streaming protocols, use a strict message framing or sequence number as part of the nonce/AAD.

---

## Error Handling

- Throws `ArgumentException` for invalid key/nonce/tag/buffer lengths
- Throws (or returns negative value) if authentication fails (never returns unauthenticated plaintext)
- Never returns `null` as a successful output

---

## Technical Details

- Poly1305 one-time key is the first 32 B of Salsa20 keystream with counter = 0
- Payload is encrypted with Salsa20 at counter = 1 and above
- MAC transcript follows [NaCl](https://nacl.cr.yp.to/) and is compatible with SecretBox
- Tag covers both ciphertext and any supplied AAD

---

## Example: End-to-end AEAD

```csharp
// Encrypt
byte[] key = ...;    // Random 16 or 32 bytes
byte[] nonce = ...;  // Unique, 8 bytes
byte[] msg = ...;
byte[] aad = ...;

byte[] cipherWithTag = Salsa20Poly1305.Encrypt(key, nonce, msg, aad);

// Decrypt (with AAD)
byte[] restored = Salsa20Poly1305.Decrypt(key, nonce, cipherWithTag, aad);
// Throws if tag/authentication fails!
```

---

## License

Licensed under the Apache License, Version 2.0.
