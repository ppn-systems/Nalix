# Tools and Utilities

The Nalix Framework provides a suite of specialized tools to accelerate development, debugging, and identity management. These tools are designed for industrial-grade performance and security.

## Core Tools

### [Identity Certificate Tool](certificate-tool.md)
A CLI utility for generating high-entropy asymmetric keys.
- **Goal**: Generate secure identities for servers and clients.
- **Algorithm**: X25519 (Curve25519).
- **Output**: Standardized `.private` and `.public` certificate files.

### [Packet Visualizer](packet-visualizer.md)
A graphical tool for inspecting and analyzing Nalix network frames in real-time.
- **Goal**: Debug protocol logic and verify payload structures.
- **Features**: Frame decoding, field highlighting, and protocol sequence visualization.

## Development Utilities

### Interoperability Tests
A proof-of-correctness suite that verifies Nalix cryptographic implementations against the [BouncyCastle](https://www.bouncycastle.org/) library.
- **Location**: `tests/Nalix.Framework.Tests/Cryptography/InteroperabilityTests.cs`.
- **Verified Primitives**: Keccak256, Poly1305, ChaCha20, Salsa20, X25519.

### Benchmarking Suite
Integrated [BenchmarkDotNet](https://benchmarkdotnet.org/) projects to verify zero-allocation goals and maintain high-throughput performance.
- **Location**: `benchmarks/`.
