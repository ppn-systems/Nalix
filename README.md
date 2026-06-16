<p align="center">
  <img src="docs/assets/!/banner.svg" alt="nalix Banner" width="100%">
  <img src="docs/assets/!/claude.svg" alt="Claude Code mascot jumping" width="120" height="100"><br>
</p>

<p align="center">
  <a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/.NET-10.0-blueviolet?logo=dotnet&logoColor=white" alt=".NET"></a>
  <a href="LICENSE"><img src="https://img.shields.io/github/license/ppn-systems/nalix?style=flat-square" alt="License"></a>
  <a href="https://www.nuget.org/packages/Nalix.Network"><img src="https://img.shields.io/nuget/v/Nalix.Network?style=flat-square&logo=nuget&label=NuGet" alt="NuGet"></a>
  <a href="https://www.nuget.org/packages/Nalix.Network"><img src="https://img.shields.io/nuget/dt/Nalix.Network?style=flat-square&logo=nuget&label=Downloads" alt="Downloads"></a>
</p>

<p align="center">
  <a href="https://github.com/ppn-systems/nalix/issues"><img src="https://img.shields.io/github/issues/ppn-systems/nalix?style=flat-square" alt="Issues"></a>
  <a href="https://github.com/ppn-systems/nalix/pulls"><img src="https://img.shields.io/github/issues-pr/ppn-systems/nalix?style=flat-square" alt="PRs"></a>
  <a href="https://github.com/ppn-systems/nalix"><img src="https://img.shields.io/github/repo-size/ppn-systems/nalix?style=flat-square" alt="Repo Size"></a>
  <a href="https://github.com/ppn-systems/nalix/commits/master"><img src="https://img.shields.io/github/commit-activity/m/ppn-systems/nalix?style=flat-square&logo=github" alt="Commit Activity"></a>
</p>

<p align="center">
  <b><a href="DOCUMENTATION.md">Documentation</a></b> · <b><a href="example/">Examples</a></b> · <b><a href="#-benchmarks">Benchmarks</a></b> · <b><a href="CONTRIBUTING.md">Contributing</a></b>
</p>

---

## 📖 About

**Nalix** is a modular, high-performance networking framework for .NET 10. It provides a complete stack for building real-time server applications — from low-level transport (TCP/UDP) to middleware pipelines, packet routing, and client SDKs — with a focus on zero-allocation hot paths, pluggable protocols, and enterprise-grade security.

---

## 🛠️ Build Status

| Platform | Status |
| :--- | :--- |
| ![Linux](https://badgen.net/badge/icon/Ubuntu%20Linux%2022.04%20x64?icon=terminal&label&color=orange) | [![CI](https://github.com/ppn-systems/nalix/actions/workflows/ci-linux.yml/badge.svg?event=push)](https://github.com/ppn-systems/nalix/actions/workflows/ci-linux.yml) |
| ![Windows](https://badgen.net/badge/icon/Windows,.NET%2010?icon=windows&label&list=1) | [![CI](https://github.com/ppn-systems/nalix/actions/workflows/ci-windows.yml/badge.svg?event=push)](https://github.com/ppn-systems/nalix/actions/workflows/ci-windows.yml) |

---

## ✨ Features

| Category | Highlights |
| :--- | :--- |
| 🖥️ **Cross-Platform** | Runs on Windows, Linux, and macOS with .NET 10+. |
| ⚡ **High Performance** | Zero-allocation serialization, shard-aware dispatch, and buffer pooling for thousands of concurrent connections. |
| 🔐 **Security-First** | AEAD encryption (ChaCha20-Poly1305), Static-Ephemeral X25519 (Noise Protocol) with server identity pinning, and zero-RTT session resumption. |
| 🔌 **Pluggable Protocols** | Swap network, serialization, or security protocols without modifying core logic. |
| 🛤️ **Middleware Pipeline** | Built-in authentication, rate limiting, traffic shaping, and audit logging — or write your own. |
| 📡 **Real-Time Updates** | Instant messaging, state synchronization, and live event broadcasting. |
| 🛠️ **Extensible** | Attribute-based packet routing, auto-discovered controllers, and fluent builder APIs. |
| 🧩 **SOLID & DDD** | Clean architecture following SOLID principles and Domain-Driven Design patterns. |
| 💻 **Modern C#** | Leverages C# 14 features — `Span<T>`, `ref struct`, pattern matching, and more. |

---

## 🔧 Requirements

| Requirement | Version |
| :--- | :--- |
| .NET SDK | [10.0+](https://dotnet.microsoft.com/download/dotnet/10.0) |
| C# Language | 14+ |
| IDE | [Visual Studio 2026](https://visualstudio.microsoft.com/downloads/) / [VS Code](https://code.visualstudio.com/) / [Rider](https://www.jetbrains.com/rider/) |

---

## 💻 Technologies

<p align="center">
  <a href="https://skillicons.dev"><img src="https://skillicons.dev/icons?i=dotnet,cs,docker,git" alt="Technologies"></a>
</p>

- **Language**: C# 14 on .NET 10
- **Testing**: xUnit + BenchmarkDotNet
- **CI/CD**: GitHub Actions (Linux & Windows)
- **Packaging**: NuGet

---

## 📈 Benchmarks

> All benchmarks run on **.NET 10.0**, **Windows 11**, using **BenchmarkDotNet v0.15.8**.

### Environment

- CPU: 13th Gen Intel Core i7-13620H (10C/16T)
- Runtime: .NET `10.0.5` (X64 RyuJIT, Server GC)
- SDK: .NET SDK `10.0.201`
- Job config: `IterationCount=20`, `LaunchCount=3`, `WarmupCount=10`, `RunStrategy=Throughput`

### 🔄 Serialization (128 items, DTO payload)

| Serializer | Serialize | Deserialize | Allocated |
| :--- | ---: | ---: | ---: |
| LiteSerializer | 149.9 ns | 142.9 ns | 664–856 B |
| MemoryPack | 121.6 ns | 145.0 ns | 664–888 B |
| MessagePack | 422.5 ns | 1,095.2 ns | 504–888 B |
| System.Text.Json | 897.7 ns | 2,548.2 ns | 1,976–7,200 B |

> **More details:** See the [`docs/benchmarks`](docs/benchmarks/) folder for full data and additional test cases.

---

## 📦 NuGet Packages

Nalix is composed of several modular packages — install only what you need.

### 🏗️ Foundation

| Package | Description |
| :--- | :--- |
| **[Nalix.Abstractions](src/Nalix.Abstractions)** | Base abstractions, enums, and shared contracts for the Nalix ecosystem. |
| **[Nalix.Codec](src/Nalix.Codec)** | High-performance framing, cryptography, and serialization. |
| **[Nalix.Environment](src/Nalix.Environment)** | Low-level IO primitives, buffer leasing, and configuration loading. |
| **[Nalix.Framework](src/Nalix.Framework)** | High-performance core: cryptography, identity, DI, serialization, and task orchestration. |
| **[Nalix.Runtime](src/Nalix.Runtime)** | Packet dispatching, middleware pipelines, protection primitives, and throttling. |

### 📡 Networking & Hosting

| Package | Description |
| :--- | :--- |
| **[Nalix.Network](src/Nalix.Network)** | High-performance TCP/UDP transport, connection management, and session persistence. |
| **[Nalix.Hosting](src/Nalix.Network.Hosting)** | Microsoft-style host and builder APIs for quick bootstrapping. |

### 🛠️ Utilities & Tooling

| Package | Description |
| :--- | :--- |
| **[Nalix.Logging](src/Nalix.Logging)** | Lightweight asynchronous logging for debugging and diagnostics. |
| **[Nalix.SDK](src/Nalix.SDK)** | Client-side SDK: transport sessions, request/response patterns, and encryption. |
| **[Nalix.Analyzers](src/Nalix.Analyzers)** | Roslyn analyzers and code fixes to enforce Nalix best practices. |
| **[Nalix.Analyzers.Generators](src/Nalix.Analyzers.Generators)** | Source generators and analyzers. |

---

## 🚀 Quick Start

Build a high-performance network application in minutes:

```csharp
using Nalix.Hosting;
using Nalix.Network.Options;

// Initialize and configure the application host
using var host = NetworkApplication.CreateBuilder()
    .ListenTcp<DefaultProtocol>().OnPort(8080).Bind()
    .ScanHandlers<Program>() // Auto-discovers all custom PacketController types in the assembly
    .Configure<NetworkSocketOptions>(opt => opt.NoDelay = true)
    .Build();

// Run the server
await host.RunAsync();
```

> See the [examples](example/) directory for complete implementation details.

---

## 📦 Installation

```bash
# Core server setup
dotnet add package Nalix.Network.Hosting

# Optional: structured logging
dotnet add package Nalix.Logging

# Optional: client SDK
dotnet add package Nalix.SDK

# Optional: Roslyn analyzers
# Optional: Roslyn analyzers generators
dotnet add package Nalix.Abstractions 
```

---

## 🛠️ Contributing

Please read [CONTRIBUTING.md](CONTRIBUTING.md) for the development workflow, commit conventions, and pull request guidelines. Follow our [Code of Conduct](CODE_OF_CONDUCT.md) and submit PRs with proper documentation and tests.

## 🛡️ Security

Please review our [Security Policy](SECURITY.md) for supported versions and vulnerability reporting procedures.

## 📜 License

Nalix is copyright &copy; PhcNguyen — provided under the [Apache License, Version 2.0](http://apache.org/licenses/LICENSE-2.0.html).

## 📬 Contact

For questions, suggestions, or support, open an issue on [GitHub](https://github.com/ppn-systems/Nalix/issues) or contact the maintainers at [ppn.system@gmail.com](mailto:ppn.system@gmail.com).

---

<p align="center">
  Give a ⭐️ if this project helped you!
  <img src="docs/assets/!/footer.svg" alt="Footer" width="100%">
</p>

<img src="docs/assets/!/divider.svg" alt="Nalix divider" width="100%">
