# Nalix Documentation

Welcome to the comprehensive technical documentation for the **Nalix** ecosystem — a modular, performance-oriented networking stack for modern .NET.

---

## Documentation Index

Nalix is composed of several specialized packages. Each module is documented with its own core concepts, API references, and usage guides.

### 🏗️ Foundation

Base libraries providing the shared contracts and runtime infrastructure.

| Module | Description | Guide |
| :--- | :--- | :---: |
| **Nalix.Common** | Shared contracts: packet/connection abstractions, protocol enums, and primitives. | [Read →](./docs/packages/nalix-common.md) |
| **Nalix.Framework** | Core utilities: cryptography, identity, DI, serialization, and task orchestration. | [Read →](./docs/packages/nalix-framework.md) |
| **Nalix.Runtime** | Execution core: packet dispatching, middleware pipelines, protection primitives, and throttling. | [Read →](./docs/packages/nalix-runtime.md) |

### 📡 Networking

High-performance transport and application hosting layers.

| Module | Description | Guide |
| :--- | :--- | :---: |
| **Nalix.Network** | Transport runtime: TCP/UDP listeners, connection management, and session persistence. | [Read →](./docs/packages/nalix-network.md) |
| **Nalix.Network.Hosting** | Fluent bootstrap: Microsoft-style host/builder APIs and managed lifecycle. | [Read →](./docs/packages/nalix-network-hosting.md) |

### 🛠️ Utilities & Tooling

Supporting subsystems and development tools.

| Module | Description | Guide |
| :--- | :--- | :---: |
| **Nalix.Logging** | High-throughput logging: asynchronous batching and pluggable sinks. | [Read →](./docs/packages/nalix-logging.md) |
| **Nalix.SDK** | Client-side: transport sessions, request helpers, and control flows. | [Read →](./docs/packages/nalix-sdk.md) |
| **Nalix.Analyzers** | Developer UX: Roslyn-based diagnostics and code fixes for Nalix best practices. | [Read →](./docs/packages/index.md#nalixanalyzers) |

---

## 🚀 Getting Started

If you are new to Nalix, we recommend following the documentation in this order:

1. **[Introduction](./docs/introduction.md)** — Core concepts and architecture overview.
2. **[Installation](./docs/installation.md)** — Setting up your development environment.
3. **[Quickstart](./docs/quickstart.md)** — Building your first Nalix application.
4. **[Examples](./example/)** — Detailed sample implementations.

---

## 📈 Performance & Benchmarks

Nalix is designed for latency-sensitive workloads. For detailed performance reports across serialization, cryptography, and networking:

- [Benchmarks Overview](./docs/benchmarks/index.md)
- [Benchmark Reports](./benchmarks/README.md)

---

## 📎 Additional Resources

| Document | Description |
| :--- | :--- |
| [README.md](./README.md) | Project overview and introduction. |
| [CHANGELOG.md](./CHANGELOG.md) | Version history and release notes. |
| [CONTRIBUTING.md](./CONTRIBUTING.md) | Development workflow and PR guidelines. |
| [SECURITY.md](./SECURITY.md) | Vulnerability reporting and supported versions. |
| [AGENTS.md](./.github/AGENTS.md) | Instructions for AI agents working in this repo. |

---

<p align="center">
  <i>"Empowering developers to build faster, safer, and smarter real-time systems."</i>
</p>
