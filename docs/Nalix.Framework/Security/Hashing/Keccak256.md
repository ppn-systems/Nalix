# Keccak256 — Fast, Zero-Allocation SHA-3-256 for .NET

**Keccak-256** (SHA-3-256) is a cryptographically secure hashing function standardized as FIPS 202.  
This implementation uses a fully inline, `ref struct`-based stack-only sponge, offering high performance and *zero* heap allocation for any input size.

- **Namespace:** `Nalix.Shared.Security.Hashing`
- **Class:** `Keccak256` (static)
- **Spec:** [FIPS 202 — SHA-3 Standard](https://nvlpubs.nist.gov/nistpubs/FIPS/NIST.FIPS.202.pdf)

---

## Features

- 256-bit (32 bytes) output; secure for digital signatures, Merkle trees, and more
- One-shot and streaming (incremental) APIs available
- All state lives *on the stack* (ref struct); no heap/GC allocation
- Padding, endianness, state layout compatible with NIST SHA-3 test vectors
- SIMD autodetection for highest throughput where available (AVX2, SSE, ARMv8)
- Constant-time, allocationless (critical for blockchain/protocol design)

---

## API Overview

| Method/Property                                 | Description                                      |
|-------------------------------------------------|--------------------------------------------------|
| `HashData(input)`                               | Returns SHA-3-256 hash (32B, allocates buffer)   |
| `HashData(input, output)`                       | Writes hash to buffer (≥32 bytes, zero alloc)    |
| (Advanced:) `Keccak256.Sponge` (ref struct)     | Incremental absorb-pad-squeeze interface         |

---

## Typical Usage

### Hash Bytes (One-shot)

```csharp
using Nalix.Shared.Security.Hashing;

byte[] data = ...;
byte[] hash = Keccak256.HashData(data);         // byte[] -> 32-byte buffer

// Or with preallocated buffer (zero allocation):
Span<byte> dst = stackalloc byte[32];
Keccak256.HashData(data, dst);
// dst now contains SHA-3-256 hash
```

### Streaming/Incremental Hash

```csharp
Keccak256.Sponge sponge = new();
sponge.Absorb(chunk1);
sponge.Absorb(chunk2);
Span<byte> hash = stackalloc byte[32];
sponge.PadAndSqueeze(hash);
```

---

## Parameters

| Name      | Value/Type | Description                                   |
|-----------|------------|-----------------------------------------------|
| output    | 32 bytes   | SHA-3-256 hash output                         |
| key/block | 136 bytes  | `RateBytes` (Keccak-256/IUF block size)       |
| input     | any        | Data to hash                                  |

---

## Notes & Best Practices

- `HashData` is zero-allocation except for *the output array* (or none with Span overload).
- If using reusable/performance-critical code, prefer Span buffers and avoid converting big data to byte[].
- The incremental API designed for hashing multiple chunks/files/message frames in-place.
- Do not share the same `Sponge` between threads; allocate new Sponge per operation.

---

## Technical Details

- Implements state width 1600 bits (25×64 lanes), Keccak-f[1600] permutation
- Rate: 1088 bits (136 bytes), Cap: 512 bits, output: 256 bits (32 bytes)
- Pad: Multi-rate 0x06/0x80 (for full NIST SHA-3)
- Endian-neutral — runs the same on little and big-endian architectures

---

## Example: Merkle Root, Address Checksum, Password Hash

```csharp
byte[] hash1 = Keccak256.HashData(data1);
byte[] hash2 = Keccak256.HashData(data2);

// Build multilayer hash, etc.
```

---

## Reference

- [FIPS 202 SHA-3 Standard](https://nvlpubs.nist.gov/nistpubs/FIPS/NIST.FIPS.202.pdf)
- [Keccak Team — keccak.team](https://keccak.team/)

---

## License

Licensed under the Apache License, Version 2.0.
