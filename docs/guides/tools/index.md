# Tools and Utilities

!!! info "Learning Signals"
    - :fontawesome-solid-layer-group: **Level**: Beginner / Intermediate
    - :fontawesome-solid-clock: **Time**: 5 minutes
    - :fontawesome-solid-book: **Prerequisites**: [Introduction](../../introduction.md)

Nalix provides a suite of specialized tools to accelerate development, debugging, and identity management. These tools ensure that your protocol logic is sound and your infrastructure is secure.

---

## 🛠️ Core Utilities

The following tools are integrated into the Nalix ecosystem to help you manage identities and inspect binary state.

<div class="grid cards" markdown>
-   :material-key-chain:{ .lg .middle } [**Certificate Tool**](certificate-tool.md)
    ---
    CLI utility for generating high-entropy X25519 identity keys for servers and authenticated clients.
    [:octicons-arrow-right-24: Generate Keys](certificate-tool.md)
-   :material-matrix:{ .lg .middle } [**Serialization Inspector**](packet-visualizer.md)
    ---
    WinForms utility to load packet DLLs and visualize how C# properties map to raw binary bytes in real-time.
    [:octicons-arrow-right-24: Inspect Bytes](packet-visualizer.md)
-   :material-toolbox:{ .lg .middle } [**SDK Developer Tools**](sdk-tools.md)
    ---
    Comprehensive WPF suite for building packets, browsing registries, and monitoring real-time logs.
    [:octicons-arrow-right-24: Open Toolbox](sdk-tools.md)

</div>

---

## 🏗️ Development Utilities

### Interoperability Tests

A proof-of-correctness suite that verifies Nalix cryptographic implementations against the [BouncyCastle](https://www.bouncycastle.org/) library.

- **Location**: `tests/Nalix.Framework.Tests/Cryptography/InteroperabilityTests.cs`.
- **Verified Primitives**: Keccak256, Poly1305, ChaCha20, Salsa20, X25519.

### Benchmarking Suite

Integrated [BenchmarkDotNet](https://benchmarkdotnet.org/) projects to verify zero-allocation goals and maintain high-throughput performance.

- **Location**: `benchmarks/`.

---

## 🚀 Recommended Path

1. :material-key: [**Setup Identity**](certificate-tool.md) — Create your first server certificate.
2. :material-application-edit: [**Inspect Packets**](packet-visualizer.md) — Verify your binary serialization.
3. :material-monitor: [**Monitor Traffic**](sdk-tools.md) — Use the SDK Tools to debug live services.
