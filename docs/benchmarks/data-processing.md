# Data Processing & Framing Benchmarks

Detailed performance metrics for the Nalix data processing and transformation pipelines, including LZ4 compression and framing transforms.

## Frame Processing Pipeline

The frame pipeline handles end-to-end outbound serialization (compression + encryption) and inbound deserialization (decryption + decompression) processes.

### Pipeline Performance (Payload Size = 64)

| Method | PayloadSize | Mean | Error | StdDev | Gen0 | Allocated |
| :--- | :--- | ---: | ---: | ---: | ---: | ---: |
| **ProcessOutbound_CompressOnly** | 64 | **208.4 ns** | 1.71 ns | 1.97 ns | 0.0062 | 232 B |
| **ProcessOutbound_EncryptOnly** | 64 | **1,200.1 ns** | 10.70 ns | 11.45 ns | 0.0057 | 232 B |
| **ProcessOutbound_Full** | 64 | **1,462.2 ns** | 14.12 ns | 15.69 ns | 0.0057 | 232 B |
| **ProcessInbound_DecompressOnly** | 64 | **252.2 ns** | 1.47 ns | 1.63 ns | 0.0114 | 432 B |
| **ProcessInbound_DecryptOnly** | 64 | **2,366.2 ns** | 27.10 ns | 31.21 ns | 0.0114 | 432 B |
| **ProcessInbound_Full** | 64 | **3,142.8 ns** | 20.47 ns | 23.58 ns | 0.0153 | 592 B |

### Pipeline Performance (Payload Size = 512)

| Method | PayloadSize | Mean | Error | StdDev | Gen0 | Allocated |
| :--- | :--- | ---: | ---: | ---: | ---: | ---: |
| **ProcessOutbound_CompressOnly** | 512 | **1,138.3 ns** | 23.68 ns | 27.27 ns | 0.0286 | 1128 B |
| **ProcessOutbound_EncryptOnly** | 512 | **3,866.7 ns** | 20.45 ns | 23.55 ns | 0.0229 | 1128 B |
| **ProcessOutbound_Full** | 512 | **5,067.3 ns** | 29.35 ns | 32.62 ns | 0.0229 | 1128 B |
| **ProcessInbound_DecompressOnly** | 512 | **1,272.2 ns** | 8.71 ns | 10.03 ns | 0.0591 | 2224 B |
| **ProcessInbound_DecryptOnly** | 512 | **7,695.9 ns** | 44.03 ns | 50.71 ns | 0.0534 | 2224 B |
| **ProcessInbound_Full** | 512 | **9,645.8 ns** | 60.95 ns | 67.75 ns | 0.0763 | 3280 B |

### Pipeline Performance (Payload Size = 4096)

| Method | PayloadSize | Mean | Error | StdDev | Gen0 | Gen1 | Allocated |
| :--- | :--- | ---: | ---: | ---: | ---: | ---: | ---: |
| **ProcessOutbound_CompressOnly** | 4096 | **8,552.0 ns** | 51.84 ns | 59.70 ns | 0.2289 | - | 8296 B |
| **ProcessOutbound_EncryptOnly** | 4096 | **25,985.8 ns** | 225.22 ns | 259.37 ns | 0.2136 | - | 8296 B |
| **ProcessOutbound_Full** | 4096 | **43,367.5 ns** | 12,069.43 ns | 13,899.17 ns | 0.1831 | - | 8296 B |
| **ProcessInbound_DecompressOnly** | 4096 | **14,000.2 ns** | 1,650.64 ns | 1,695.08 ns | 2.3651 | 0.0610 | 16560 B |
| **ProcessInbound_DecryptOnly** | 4096 | **97,138.0 ns** | 5,849.07 ns | 6,735.79 ns | 0.4272 | - | 16560 B |
| **ProcessInbound_Full** | 4096 | **114,865.6 ns** | 8,360.09 ns | 9,627.49 ns | 0.6104 | - | 24784 B |

### Behind the design

- **Linear Scaling**: Latency scales predictably relative to the payload size.
- **Pipelined Execution**: Outbound processing streams data directly, enabling single-digit microsecond framing for common payload sizes (e.g. 5.06 μs for a 512B payload).

---

## Frame Transformations

Individual pipeline transformers handle compression/decompression and encryption/decryption routines.

### Transformer Performance (Payload Size = 64)

| Method | PayloadSize | Mean | Error | StdDev | P95 | Gen0 | Allocated |
| :--- | :--- | ---: | ---: | ---: | ---: | ---: | ---: |
| **Encrypt_AEAD_ChaCha20Poly1305** | 64 | **2,269.9 ns** | 159.82 ns | 177.64 ns | 2,466.2 ns | - | 64 B |
| **Decrypt_AEAD_ChaCha20Poly1305** | 64 | **4,573.8 ns** | 168.75 ns | 194.33 ns | 4,846.7 ns | 0.0038 | 96 B |
| **Encrypt_AEAD_Salsa20Poly1305** | 64 | **1,611.5 ns** | 56.27 ns | 64.80 ns | 1,691.9 ns | - | 64 B |
| **Decrypt_AEAD_Salsa20Poly1305** | 64 | **3,068.9 ns** | 227.32 ns | 261.79 ns | 3,291.3 ns | 0.0038 | 96 B |
| **Encrypt_Symmetric_ChaCha20** | 64 | **1,006.9 ns** | 83.08 ns | 95.68 ns | 1,163.9 ns | 0.0010 | 64 B |
| **Decrypt_Symmetric_ChaCha20** | 64 | **1,666.3 ns** | 119.77 ns | 137.93 ns | 1,961.5 ns | 0.0019 | 96 B |
| **Encrypt_Symmetric_Salsa20** | 64 | **594.2 ns** | 17.52 ns | 18.74 ns | 620.0 ns | 0.0024 | 64 B |
| **Decrypt_Symmetric_Salsa20** | 64 | **1,444.2 ns** | 302.76 ns | 348.66 ns | 2,197.5 ns | 0.0019 | 96 B |
| **Compress_LZ4** | 64 | **420.8 ns** | 15.55 ns | 17.90 ns | 437.5 ns | 0.0014 | 64 B |
| **Decompress_LZ4** | 64 | **546.4 ns** | 21.44 ns | 24.69 ns | 577.8 ns | 0.0024 | 96 B |

### Transformer Performance (Payload Size = 1024)

| Method | PayloadSize | Mean | Error | StdDev | P95 | Allocated |
| :--- | :--- | ---: | ---: | ---: | ---: | ---: |
| **Encrypt_AEAD_ChaCha20Poly1305** | 1024 | **11,988.0 ns** | 312.14 ns | 359.46 ns | 12,406.5 ns | 64 B |
| **Decrypt_AEAD_ChaCha20Poly1305** | 1024 | **23,855.6 ns** | 849.48 ns | 978.27 ns | 25,185.6 ns | 96 B |
| **Encrypt_AEAD_Salsa20Poly1305** | 1024 | **9,260.9 ns** | 196.40 ns | 201.69 ns | 9,430.6 ns | 64 B |
| **Decrypt_AEAD_Salsa20Poly1305** | 1024 | **17,336.5 ns** | 510.60 ns | 588.01 ns | 18,040.1 ns | 96 B |
| **Encrypt_Symmetric_ChaCha20** | 1024 | **6,997.8 ns** | 585.59 ns | 674.37 ns | 8,219.4 ns | 64 B |
| **Decrypt_Symmetric_ChaCha20** | 1024 | **14,080.3 ns** | 656.06 ns | 755.52 ns | 15,281.7 ns | 96 B |
| **Encrypt_Symmetric_Salsa20** | 1024 | **3,575.7 ns** | 80.63 ns | 92.85 ns | 3,693.7 ns | 64 B |
| **Decrypt_Symmetric_Salsa20** | 1024 | **6,906.9 ns** | 172.10 ns | 198.19 ns | 7,111.3 ns | 96 B |
| **Compress_LZ4** | 1024 | **3,845.4 ns** | 159.29 ns | 183.44 ns | 4,051.2 ns | 64 B |
| **Decompress_LZ4** | 1024 | **4,043.7 ns** | 105.77 ns | 121.81 ns | 4,202.6 ns | 96 B |

### Why Nalix Data Processing?

- **Zero-Allocation Transforms**: Rather than creating intermediate garbage arrays, encryption and compression operate directly on rentals from `BufferPoolManager` using `Span<byte>`, consuming only minimal tracking object references (~64 B for encryption, ~96 B for decryption).
- **Stream Cipher Dominance**: Salsa20 performs significantly better than ChaCha20 on standard CPUs, completing symmetric encryption of 1 KB of data in ~3.5 μs compared to ~7 μs for ChaCha20.
- **LZ4 Inline Compression**: Built-in LZ4 compression integrates seamlessly with the buffer pipeline, performing a 1 KB compression in under 3.9 μs.
