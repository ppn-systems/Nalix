# Tools and Utilities

!!! info "Learning Signals"
    - :fontawesome-solid-layer-group: **Level**: Beginner / Intermediate
    - :fontawesome-solid-clock: **Time**: 5 minutes
    - :fontawesome-solid-book: **Prerequisites**: [Introduction](../../introduction.md)

Nalix currently ships one end-user tool in this repository and documents a few adjacent debugging workflows. This section reflects the current source tree so readers do not chase missing projects.

---

## Core Utilities

The following pages either map to a bundled tool or explain the equivalent workflow with the APIs available in `src/`.

<div class="grid cards" markdown>
-   :material-key-chain:{ .lg .middle } [**Certificate Tool**](certificate-tool.md)
    ---
    Bundled CLI utility for generating X25519 identity keys for servers and client pinning.
    [:octicons-arrow-right-24: Generate Keys](certificate-tool.md)
-   :material-matrix:{ .lg .middle } [**Serialization Inspector**](packet-visualizer.md)
    ---
    Serialization inspection workflow for the current codebase. The historical desktop visualizer is not present in this repo snapshot.
    [:octicons-arrow-right-24: Inspect Serialization](packet-visualizer.md)
-   :material-toolbox:{ .lg .middle } [**SDK Developer Tools**](sdk-tools.md)
    ---
    Status page for the historical desktop toolbox, with supported alternatives for the current repo.
    [:octicons-arrow-right-24: View Status](sdk-tools.md)

</div>

---

## Development Utilities

### Interoperability Tests

A proof-of-correctness suite that verifies Nalix cryptographic implementations against the [BouncyCastle](https://www.bouncycastle.org/) library.

- **Location**: `tests/Nalix.Framework.Tests/Cryptography/InteroperabilityTests.cs`.
- **Verified Primitives**: Keccak256, Poly1305, ChaCha20, Salsa20, X25519.

### Benchmarking Suite

Integrated [BenchmarkDotNet](https://benchmarkdotnet.org/) projects to verify zero-allocation goals and maintain high-throughput performance.

- **Location**: `benchmarks/`.

---

## Recommended Path

1. :material-key: [**Setup Identity**](certificate-tool.md) — Create your first server certificate.
