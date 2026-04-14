# Cryptography

Nalix ships several cryptography primitives in `Nalix.Framework.Security`, but they are easier to read as separate topics than as one long page.

## Source mapping

- `src/Nalix.Framework/Security/Asymmetric`
- `src/Nalix.Framework/Security/Hashing`
- `src/Nalix.Framework/Security/Symmetric`
- `src/Nalix.Framework/Security/Aead`
- `src/Nalix.Framework/Security/Engine`
- `src/Nalix.Framework/Security/Primitives`
- `src/Nalix.Framework/Security/EnvelopeCipher.cs`
- `src/Nalix.Framework/Security/HandshakeX25519.cs`

## What is in this package

| Topic | Main types | Read next |
|---|---|---|
| Hashing and MAC | `Keccak256`, `Poly1305` | [Hashing and MAC](./hashing-and-mac.md) |
| AEAD and envelope encryption | `ChaCha20Poly1305`, `Salsa20Poly1305`, `EnvelopeCipher` | [AEAD and Envelope](./aead-and-envelope.md) |
| Handshake protocol | `HandshakeHandlers`, `X25519` | [Handshake Protocol](./handshake.md) |

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

- `src/Nalix.Framework/Random/Csprng.cs`

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
- [UDP Auth Flow](../../guides/udp-auth-flow.md)
