# Cryptography

Nalix ships several cryptography primitives in `Nalix.Codec.Security`, but they are easier to read as separate topics than as one long page.

## Source mapping

- `src/Nalix.Codec/Security/Asymmetric`
- `src/Nalix.Codec/Security/Hashing`
- `src/Nalix.Codec/Security/Symmetric`
- `src/Nalix.Codec/Security/Aead`
- `src/Nalix.Codec/Security/Engine`
- `src/Nalix.Abstractions/Primitives`
- `src/Nalix.Codec/Security/Hashing/HmacKeccak256.cs`
- `src/Nalix.Codec/Security/Hashing/Pbkdf2.cs`
- `src/Nalix.Codec/Security/EnvelopeCipher.cs`
- `src/Nalix.Codec/Security/HandshakeX25519.cs`
- `src/Nalix.Abstractions/Security/CipherSuiteType.cs`
- `src/Nalix.Abstractions/Security/DropPolicy.cs`

## What is in this package

| Topic | Main types | Read next |
| --- | --- | --- |
| Hashing and MAC | `Keccak256`, `HmacKeccak256`, `Poly1305`, `Pbkdf2` | [Hashing and MAC](./hashing-and-mac.md) |
| AEAD and envelope encryption | `ChaCha20Poly1305`, `Salsa20Poly1305`, `EnvelopeCipher` | [AEAD and Envelope](./aead-and-envelope.md) |
| Handshake protocol | `HandshakeHandlers`, `X25519` | [Handshake Protocol](./handshake.md) |
| Security enums | `CipherSuiteType`, `DropPolicy` | |

## Quick guidance

- use `X25519` for session key agreement
- use `Keccak256` for transcript hashing and proofs
- use `EnvelopeCipher` when you want the high-level transport-facing encryption entry point
- use `Pbkdf2` for credential hashing helpers
- use `Csprng` when you need secure random bytes, nonces, or unbiased random integers

## Quick example

```csharp
var keys = X25519.GenerateKeyPair();
byte[] digest = Keccak256.HashData(payload);

Pbkdf2.Hash("secret", out byte[] salt, out byte[] hash);
```

## Randomness helper

Nalix also ships `Csprng` in `Nalix.Framework.Random`:

- `src/Nalix.Environment/Random/Csprng.cs`

Use it for:

- secure byte generation with `GetBytes(...)` or `Fill(...)`
- nonce generation with `CreateNonce(...)`
- unbiased integer sampling with `GetInt32(...)`
- strict cryptographic randomness only; if the operating system CSPRNG cannot be initialized, Nalix throws instead of downgrading to a non-cryptographic fallback

### Quick example

```csharp
byte[] key = Csprng.GetBytes(32);
byte[] nonce = Csprng.CreateNonce();
int shard = Csprng.GetInt32(0, 8);
```

## Related APIs

- [Hashing and MAC](./hashing-and-mac.md)
- [AEAD and Envelope](./aead-and-envelope.md)
- [Handshake Extensions](../sdk/handshake-extensions.md)
- [UDP Security Guide](../../guides/networking/udp-security.md)
