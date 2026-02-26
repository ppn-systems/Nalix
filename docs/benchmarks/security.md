# Security & Cryptography Benchmarks

Detailed performance metrics for Nalix security primitives, including encryption engines and hashing algorithms.

## Encryption Engines
High-level engines for envelope and AEAD (Authenticated Encryption with Associated Data) operations.

| Engine | Operation | Latency (64B) | Latency (1KB) | StdDev |
| :--- | :--- | :--- | :--- | :--- |
| **Symmetric Engine** | Encrypt | **270.8 ns** | **2.73 μs** | 2.33 ns |
| **Symmetric Engine** | Decrypt | **275.4 ns** | **2.74 μs** | 2.54 ns |
| **AEAD Engine** | Encrypt | **1.16 μs** | **6.41 μs** | 5.91 ns |
| **AEAD Engine** | Decrypt | **1.20 μs** | **6.47 μs** | 19.48 ns |

### Why Nalix Security?
Security is built into the core transmission pipeline with zero allocation overhead and near-hardware speeds.

- **Software-Efficient Stream Ciphers**: Nalix utilizes **Salsa20** and **ChaCha20** for symmetric encryption. These ciphers are designed for massive throughput on modern CPUs without requiring hardware-specific AES-NI instructions.
- **Bitwise Precision**: Encryption engines utilize `BitOperations.RotateLeft` and intensive bit-shifting to achieve maximum throughput. The state is maintained in `stackalloc` memory to ensure local cache hit rates and zero heap pressure.
- **AEAD Protection**: All transmission packets can be wrapped in an Encrypt-then-MAC (EtM) envelope, providing authenticated encryption with associated data (AEAD) to prevent tampering and replay attacks.
- **High-Performance Hashing**: Utilizes **CRC32C** and **XXHash64** for non-cryptographic payload integrity, achieving 10GB/s+ processing speeds per CPU core.

---

## Envelope Cipher Suites
Support for modern stream ciphers and MACs.

| Suite | Operation | Latency (64B Mean) | StdDev |
| :--- | :--- | :--- | :--- |
| **Salsa20** (Stream) | Decrypt | **153.3 ns** | 3.68 ns |
| **ChaCha20** (Stream) | Decrypt | **287.4 ns** | 7.85 ns |
| **Salsa20-Poly1305** | AEAD Verify | **848.6 ns** | 2.19 ns |
| **ChaCha20-Poly1305** | AEAD Verify | **1.14 μs** | 4.54 ns |

### Design Strategy
- **One-Pass AEAD**: The combined `Cipher-Poly1305` suites perform both authentication and decryption in a single pass over the memory, reducing cache misses.
- **Envelope Encryption**: Data keys are rotated per session, but the performance cost of session key derivation is isolated from the hot data path.

---

## Hashing & Randomness
Foundational primitives for data integrity and high-entropy security.

| Operation | Primitive | Latency (Mean) | StdDev |
| :--- | :--- | :--- | :--- |
| **Hash Verification** | Poly1305 | **157.5 ns** | 0.27 ns |
| **Hash Computation** | Keccak256 | **430.8 ns** | 0.91 ns |
| **Random UInt64** | CSPRNG | **44.83 ns** | 0.18 ns |
| **Non-blocking Nonce** | CSPRNG | **52.24 ns** | 0.53 ns |

### Security Primitives
- **Non-blocking CSPRNG**: The random number generator is designed to avoid OS-level entropy starvation by maintaining a fast local entropy pool, critical for generating high-frequency nonces (~52ns).
- **KECCAK Speed**: The Keccak256 implementation utilizes SIMD optimizations to process hashes with sub-microsecond latency, ideal for verifying packet integrity.
