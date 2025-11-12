# EnvelopeCipher — Unified AEAD & Stream Cipher Envelope for .NET

**EnvelopeCipher** provides a convenient, high-level API for envelope encryption with modern AEAD ciphers (like ChaCha20-Poly1305, Salsa20-Poly1305) and pure stream/CTR ciphers (like ChaCha20, Salsa20).  
It encodes an envelope: `header || nonce || ciphertext [|| tag]` (for AEAD) suitable for wire protocols, messaging, files, and system-level integration.

- **Namespace:** `Nalix.Shared.Security`
- **Class:** `EnvelopeCipher` (static)
- **Design:** Stateless — fully thread-safe, multi-platform

---

## Features

- Secure envelope structure for message transmission/storage
- AEAD suites (`CHACHA20_POLY1305`, `SALSA20_POLY1305`): authenticated ciphertext, detached tag
- Stream/CTR suites (`CHACHA20`, `SALSA20`): only confidentiality, no tag
- Auto-generates secure random nonces for each envelope (you don't manage nonces manually)
- Supports Associated Data (AAD) with AEAD (e.g. protocol headers, extra metadata)
- `GetNonceLength`, `GetTagLength` utilities avoid buffer/math errors
- Fails securely: returns `false` (not exceptions) on parse/auth failure

---

## Envelope Format

- **AEAD**: `header || nonce || ciphertext || tag`
- **STREAM/CTR**: `header || nonce || ciphertext`
- **Header**: Contains protocol meta, sequence/counter, etc.

---

## API Overview

| Method/Property                                     | Description                                          |
|-----------------------------------------------------|------------------------------------------------------|
| `GetNonceLength(CipherSuiteType type)`              | Returns nonce/IV size for each cipher                |
| `GetTagLength(CipherSuiteType type)`                | Returns tag size (0 for stream ciphers)              |
| `Encrypt(key, plain, out envelope, ...)`            | Writes envelope (secure random nonce); supports AAD  |
| `Decrypt(key, envelope, out plain, ...)`            | Parses and authenticates; fails-secure on error      |

---

## Typical Usage

### Envelope AEAD Encryption

```csharp
using Nalix.Shared.Security;

byte[] key  = ...; // 32 bytes (use CSPRNG!)
byte[] data = ...; // plain message
byte[] aad  = ...; // optional, for AEAD ciphers only

Span<byte> envelope = stackalloc byte[data.Length + 64]; // big enough
EnvelopeCipher.Encrypt(key, data, envelope, aad, null, CipherSuiteType.CHACHA20_POLY1305, out int written);
// envelope[..written] ready to transmit/copy/store
```

### Envelope Decryption

```csharp
Span<byte> plain = stackalloc byte[envelope.Length];
bool ok = EnvelopeCipher.Decrypt(key, envelope, plain, aad, out int ptWritten);
if (!ok) throw new SecurityException("Auth failed (tag mismatch, tamper, invalid header/nonce)");
```

> **Note:** AEAD ciphers require `aad` passed consistently in both encrypt & decrypt.

---

## Parameters

| Name         | Type / Size            | Description                                     |
|--------------|------------------------|-------------------------------------------------|
| `key`        | byte[] (16/32 bytes)   | Secret key (suite-specific length)              |
| `plain`      | byte[]/Span<`byte`>    | Plaintext to encrypt/decrypt                    |
| `envelope`   | Span<`byte`>           | Envelope output (header\|\|nonce\|\|ct[\|\|tag])|
| `aad`        | byte[]/Span<`byte`>    | Optional Associated Data (only for AEAD)        |
| `seq`        | UInt32?                | Optional sequence/counter (header use)          |
| `algorithm`  | CipherSuiteType        | Suite: CHACHA20, SALSA20, *+POLY1305, etc.      |
| `written`    | out int                | Actual #bytes written to output                 |

---

## Security Guidance

- **Nonces**: Random per envelope (auto-handled). Never reuse nonce/key pairs!
- **AAD**: Any unencrypted data in your protocol/message should be authenticated as AAD.
- **Tag**: Never ignore the tag result in AEAD. Always check return `bool` from `.Decrypt`.
- Only use stream/CTR modes for legacy compatibility; AEAD modes are *strongly* preferred.

---

## Error Handling

- `.Encrypt`/`.Decrypt` return `bool`: Success/fail (for auth tag or parse/format errors)
- Incorrect buffer/key/nonce sizes may throw `ArgumentException`
- On *decryption failure*, output is zeroed and not usable

---

## Example: Secure Messaging Envelope

```csharp
// Encrypt for sending
EnvelopeCipher.Encrypt(key, plain, envelope, aad, seq, CipherSuiteType.CHACHA20_POLY1305, out int encLen);
// send envelope[..encLen]

// Decrypt after receiving
byte[] plain = new byte[envelope.Length]; // or stackalloc if max size known
bool ok = EnvelopeCipher.Decrypt(key, envelope, plain, aad, out int plainLen);
if (!ok) throw new Exception("Envelope failed authentication");
```

---

## Reference

- [RFC 8439 — ChaCha20 & Poly1305 for AEAD and Streaming](https://datatracker.ietf.org/doc/html/rfc8439)
- [AEAD: Authenticated Encryption Explained (Cryptography Stack Exchange)](https://crypto.stackexchange.com/q/2021)

---

## License

Licensed under the Apache License, Version 2.0.
