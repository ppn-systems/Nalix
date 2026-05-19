# Security & Cryptography Benchmarks

Detailed performance metrics for the Nalix security primitives, including encryption ciphers, handshake computation, and hashing algorithms.

## Envelope Cipher Suites

Comparison of stream ciphers (Salsa20 and ChaCha20) and AEAD suites (Salsa20-Poly1305 and ChaCha20-Poly1305) over 64 B and 1024 B payloads. All operations are run on zero-allocation spans.

### Envelope Cipher Performance (Payload Size = 64)

| Method | PayloadSize | Mean | Error | StdDev | P95 | Allocated |
| :--- | :--- | ---: | ---: | ---: | ---: | ---: |
| **Encrypt_Salsa20** | 64 | **338.3 ns** | 6.63 ns | 7.64 ns | 343.6 ns | 0 B |
| **Decrypt_Salsa20** | 64 | **209.3 ns** | 41.26 ns | 47.51 ns | 268.9 ns | 0 B |
| **Encrypt_Chacha20** | 64 | **542.6 ns** | 128.91 ns | 148.45 ns | 689.0 ns | 0 B |
| **Decrypt_Chacha20** | 64 | **572.5 ns** | 14.77 ns | 17.01 ns | 590.5 ns | 0 B |
| **Encrypt_Salsa20Poly1305** | 64 | **1,441.5 ns** | 7.87 ns | 9.06 ns | 1,448.3 ns | 0 B |
| **Decrypt_Salsa20Poly1305** | 64 | **1,339.4 ns** | 4.15 ns | 4.61 ns | 1,340.1 ns | 0 B |
| **Encrypt_Chacha20Poly1305** | 64 | **1,985.3 ns** | 8.84 ns | 9.46 ns | 1,983.9 ns | 0 B |
| **Decrypt_Chacha20Poly1305** | 64 | **2,127.8 ns** | 7.92 ns | 8.80 ns | 2,127.0 ns | 0 B |

### Envelope Cipher Performance (Payload Size = 1024)

| Method | PayloadSize | Mean | Error | StdDev | P95 | Allocated |
| :--- | :--- | ---: | ---: | ---: | ---: | ---: |
| **Encrypt_Salsa20** | 1024 | **3,323.3 ns** | 18.41 ns | 21.20 ns | 3,346.6 ns | 0 B |
| **Decrypt_Salsa20** | 1024 | **3,238.2 ns** | 74.87 ns | 83.22 ns | 3,274.5 ns | 0 B |
| **Encrypt_Chacha20** | 1024 | **7,069.6 ns** | 87.60 ns | 100.89 ns | 7,185.2 ns | 0 B |
| **Decrypt_Chacha20** | 1024 | **6,927.2 ns** | 110.46 ns | 127.20 ns | 7,120.8 ns | 0 B |
| **Encrypt_Salsa20Poly1305** | 1024 | **9,405.3 ns** | 23.86 ns | 26.53 ns | 9,397.4 ns | 0 B |
| **Decrypt_Salsa20Poly1305** | 1024 | **8,600.0 ns** | 18.80 ns | 19.31 ns | 8,601.3 ns | 0 B |
| **Encrypt_Chacha20Poly1305** | 1024 | **12,580.6 ns** | 83.37 ns | 96.01 ns | 12,606.7 ns | 0 B |
| **Decrypt_Chacha20Poly1305** | 1024 | **13,594.1 ns** | 72.70 ns | 83.72 ns | 13,718.0 ns | 0 B |

### Behind the design

- **Software-Efficient Stream Ciphers**: Salsa20 and ChaCha20 perform encryption at near-hardware speeds. The state is maintained in `stackalloc` memory, ensuring zero allocations.
- **One-Pass AEAD**: The combined ciphers with Poly1305 perform authentication and decryption in a single pass over the memory block, minimizing cache misses.

---

## Handshake Protocol

Performance metrics for the cryptographic handshake phase, establishing session identity.

| Method | Mean | Error | StdDev | P95 | Allocated |
| :--- | ---: | ---: | ---: | ---: | ---: |
| **ComputeMasterSecret** | **2.339 μs** | 0.2225 μs | 0.2562 μs | 2.564 μs | 0 B |
| **ComputeServerProof** | **2.076 μs** | 0.0301 μs | 0.0347 μs | 2.137 μs | 0 B |
| **ComputeClientProof** | **2.049 μs** | 0.0209 μs | 0.0233 μs | 2.085 μs | 0 B |
| **DeriveSessionKey** | **2.038 μs** | 0.0336 μs | 0.0387 μs | 2.090 μs | 0 B |

### Behind the design

- **Zero-Allocation Keys**: High-cost key computations take ~2 μs each and run completely allocation-free (0 B), isolating cryptographic handshakes from garbage collection impacts.

---

## Hashing & Cryptography

Comparison of cryptographic hashing, verification ciphers, and key derivation algorithms.

### Hashing Performance (Payload Size = 64)

| Method | PayloadSize | Mean | Error | StdDev | P95 | Allocated |
| :--- | :--- | ---: | ---: | ---: | ---: | ---: |
| **Keccak256_Hash** | 64 | **497.6 ns** | 6.33 ns | 7.29 ns | 509.4 ns | 0 B |
| **HmacKeccak256_Compute** | 64 | **2,444.6 ns** | 262.97 ns | 302.84 ns | 2,744.5 ns | 0 B |
| **Poly1305_Compute** | 64 | **392.8 ns** | 12.37 ns | 14.25 ns | 414.8 ns | 0 B |
| **Pbkdf2_Hash** | 64 | **2,715,123.8 ns** | 76,872.20 ns | 88,526.14 ns | 2,867,094.5 ns | 312 B |
| **Pbkdf2_Verify** | 64 | **2,782,720.5 ns** | 108,096.06 ns | 124,483.59 ns | 2,934,504.6 ns | 256 B |

### Hashing Performance (Payload Size = 1024)

| Method | PayloadSize | Mean | Error | StdDev | P95 | Allocated |
| :--- | :--- | ---: | ---: | ---: | ---: | ---: |
| **Keccak256_Hash** | 1024 | **4,128.4 ns** | 41.92 ns | 44.85 ns | 4,204.4 ns | 0 B |
| **HmacKeccak256_Compute** | 1024 | **5,846.4 ns** | 89.48 ns | 95.75 ns | 5,967.4 ns | 0 B |
| **Poly1305_Compute** | 1024 | **3,703.3 ns** | 110.55 ns | 122.88 ns | 3,940.0 ns | 0 B |
| **Pbkdf2_Hash** | 1024 | **2,135,715.8 ns** | 35,510.49 ns | 40,893.93 ns | 2,199,845.8 ns | 312 B |
| **Pbkdf2_Verify** | 1024 | **2,063,350.6 ns** | 22,490.51 ns | 24,064.59 ns | 2,109,285.6 ns | 256 B |

### Behind the design

- **Keccak Optimization**: Our custom Keccak256 implementation hashes small structures in under 500 ns and 1KB structures in 4.1 μs without allocating memory.
- **PBKDF2 Overhead & Isolation**: PBKDF2 is a secure key derivation function designed to be intentionally computationally expensive (taking ~2.7 ms per operation). Due to its CPU-bound nature, PBKDF2 checks are isolated from the networking hot path to prevent starvation of the listener thread pool.
