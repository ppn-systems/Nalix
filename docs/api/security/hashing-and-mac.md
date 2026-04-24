# Hashing and MAC

This page covers the hashing and message-authentication primitives exposed by `Nalix.Framework.Security`.

## Source mapping

- `src/Nalix.Codec/Security/Hashing/Keccak256.cs`
- `src/Nalix.Codec/Security/Hashing/Poly1305.cs`

## Main types

- `Keccak256`
- `Poly1305`

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

## Poly1305

`Poly1305` is the MAC primitive used by the detached AEAD implementations.

You usually do not call it directly from Nalix transport code. It is mostly consumed by:

- `ChaCha20Poly1305`
- `Salsa20Poly1305`
- `EnvelopeCipher` through the AEAD engines

## Related APIs

- [Cryptography](./cryptography.md)
- [AEAD and Envelope](./aead-and-envelope.md)
