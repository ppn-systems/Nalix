# Cryptography

Nalix ships several cryptography primitives in `Nalix.Framework.Security`, but they are easier to read as separate topics than as one long page.

## Source mapping

- `src/Nalix.Framework/Security/Asymmetric/*`
- `src/Nalix.Framework/Security/Hashing/*`
- `src/Nalix.Framework/Security/Symmetric/*`
- `src/Nalix.Framework/Security/Aead/*`
- `src/Nalix.Framework/Security/Engine/*`
- `src/Nalix.Framework/Security/Credentials/*`
- `src/Nalix.Framework/Security/EnvelopeCipher.cs`

## What is in this package

| Topic | Main types | Read next |
|---|---|---|
| Hashing and MAC | `Keccak256`, `Poly1305` | [Hashing and MAC](./hashing-and-mac.md) |
| AEAD and envelope encryption | `ChaCha20Poly1305`, `Salsa20Poly1305`, `EnvelopeCipher` | [AEAD and Envelope](./aead-and-envelope.md) |

## Quick guidance

- use `X25519` for session key agreement
- use `Keccak256` only when you explicitly want Keccak, not FIPS SHA3
- use `EnvelopeCipher` when you want the high-level transport-facing encryption entry point
- use `Pbkdf2` for credential hashing helpers
- use `Csprng` when you need secure random bytes, nonces, or unbiased random integers
- treat `Ed25519` as legacy because it is marked obsolete in source

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

### Quick example

```csharp
byte[] key = Csprng.GetBytes(32);
byte[] nonce = Csprng.CreateNonce();
int shard = Csprng.GetInt32(0, 8);
```

## Related APIs

- [Hashing and MAC](./hashing-and-mac.md)
- [AEAD and Envelope](./aead-and-envelope.md)
- [SDK Extensions](../sdk/tcp-session-extensions.md)
- [UDP Auth Flow](../../guides/udp-auth-flow.md)
