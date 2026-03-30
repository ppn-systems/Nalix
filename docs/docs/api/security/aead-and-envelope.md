# AEAD and Envelope

This page covers the encryption primitives that matter most to transport and packet protection.

## Source mapping

- `src/Nalix.Framework/Security/Aead/ChaCha20Poly1305.cs`
- `src/Nalix.Framework/Security/Aead/Salsa20Poly1305.cs`
- `src/Nalix.Framework/Security/Engine/AeadEngine.cs`
- `src/Nalix.Framework/Security/Engine/SymmetricEngine.cs`
- `src/Nalix.Framework/Security/Symmetric/ChaCha20.cs`
- `src/Nalix.Framework/Security/Symmetric/Salsa20.cs`
- `src/Nalix.Framework/Security/EnvelopeCipher.cs`

## Main types

- `ChaCha20Poly1305`
- `Salsa20Poly1305`
- `EnvelopeCipher`

## AEAD primitives

`ChaCha20Poly1305` and `Salsa20Poly1305` are detached-tag implementations.

They currently:

- take spans first, with minimal-allocation overloads
- authenticate `AAD || pad16 || ciphertext || pad16 || lengths`
- verify the tag before returning decrypted data

## Size rules from source

| Type | Key size | Nonce size | Tag size |
|---|---:|---:|---:|
| `ChaCha20Poly1305` | `32` | `12` | `16` |
| `Salsa20Poly1305` | `16` or `32` | `8` | `16` |

## EnvelopeCipher

`EnvelopeCipher` is the high-level encryption facade used by transport-facing code.

It dispatches by `CipherSuiteType` and hides whether the selected suite is:

- AEAD: `header || nonce || ciphertext || tag`
- stream/symmetric: `header || nonce || ciphertext`

## Basic usage

```csharp
Span<byte> ciphertext = stackalloc byte[plaintext.Length + EnvelopeCipher.HeaderSize + 32];

bool encrypted = EnvelopeCipher.Encrypt(
    key,
    plaintext,
    ciphertext,
    aad,
    seq: null,
    algorithm: CipherSuiteType.CHACHA20_POLY1305,
    out int written);
```

## Current runtime behavior

- `GetNonceLength(...)` and `GetTagLength(...)` expose suite-dependent sizing
- AEAD suites route into `AeadEngine`
- non-AEAD suites route into `SymmetricEngine`
- decryption returns `false` on parse or authentication failure instead of throwing
- AEAD encryption generates a fresh random nonce internally per call

## Related APIs

- [Cryptography](./cryptography.md)
- [Hashing and MAC](./hashing-and-mac.md)
- [Transport Options](../sdk/options/transport-options.md)
- [UDP Auth Flow](../../guides/udp-auth-flow.md)
