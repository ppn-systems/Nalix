# Hashing and MAC

This page covers the hashing and message-authentication primitives exposed by `Nalix.Codec.Security`.

## Source mapping

- `src/Nalix.Codec/Security/Hashing/Keccak256.cs`
- `src/Nalix.Codec/Security/Hashing/HmacKeccak256.cs`
- `src/Nalix.Codec/Security/Hashing/Poly1305.cs`
- `src/Nalix.Codec/Security/Hashing/Pbkdf2.cs`

## Main types

- `Keccak256`
- `HmacKeccak256`
- `Poly1305`
- `Pbkdf2`

## Keccak256

`Keccak256` is the hash primitive used in source for key derivation and signing-related flows.

## Basic usage

```csharp
byte[] digest = Keccak256.HashData(payload);
```

## Important behavior

- this is `Keccak-256`, not FIPS `SHA3-256`
- the implementation uses the original Keccak domain padding byte `0x01`
- span-based overloads exist for allocation-sensitive paths
- the implementation contains SIMD-aware fast paths for supported platforms

If another system expects SHA3-256, do not assume compatibility.

## HmacKeccak256

`HmacKeccak256` provides an HMAC construction using Keccak-256 as the underlying hash.

### Basic usage

```csharp
Span<byte> output = stackalloc byte[32];
HmacKeccak256.Compute(key, data, output);
```

### Important behavior

- uses the original Keccak-256 (not FIPS SHA3-256)
- block size is 136 bytes, hash size is 32 bytes
- keys longer than the block size are hashed down with `Keccak256`
- all intermediate buffers are cleared from the stack after computation

## Poly1305

`Poly1305` is the MAC primitive used by the detached AEAD implementations.

You usually do not call it directly from Nalix transport code. It is mostly consumed by:

- `ChaCha20Poly1305`
- `Salsa20Poly1305`
- `EnvelopeCipher` through the AEAD engines

## Pbkdf2

`Pbkdf2` provides secure credential hashing and verification using PBKDF2_I.

### Basic usage

```csharp
Pbkdf2.Hash("secret", out byte[] salt, out byte[] hash);

bool valid = Pbkdf2.Verify("secret", salt, hash);
```

### Encoded credentials

`Pbkdf2.Encoded` wraps salt and hash into a single Base64 string with version information:

```csharp
string encoded = Pbkdf2.Encoded.Hash("secret");
bool valid = Pbkdf2.Encoded.Verify("secret", encoded);
```

### Important behavior

- key size and salt size are both 32 bytes
- iteration count is loaded from `SecurityOptions` via `ConfigurationManager`, defaulting to 310,000
- verification uses constant-time comparison

## Related APIs

- [Cryptography](./cryptography.md)
- [AEAD and Envelope](./aead-and-envelope.md)
