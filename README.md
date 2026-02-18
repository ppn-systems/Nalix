# ![Icon](https://raw.githubusercontent.com/ppn-systems/Nalix/refs/heads/master/docs/assets/nalix.ico) **Nalix**

![.NET](https://img.shields.io/badge/.NET-10.0-blueviolet?logo=dotnet&logoColor=white)

![GitHub License](https://img.shields.io/github/license/phcnguyen/Nalix?style=flat-square)
![NuGet Version](https://img.shields.io/nuget/v/Nalix.Common?style=flat-square&logo=nuget)
![NuGet Downloads](https://img.shields.io/nuget/dt/Nalix.Common?style=flat-square&logo=nuget)

![Issues](https://img.shields.io/github/issues/ppn-systems/Nalix)
![PRs](https://img.shields.io/github/issues-pr/ppn-systems/Nalix)
![GitHub file size in bytes](https://img.shields.io/github/repo-size/ppn-systems/Nalix)
![GitHub commit activity](https://img.shields.io/github/commit-activity/m/phcnguyen/Nalix?style=flat-square&logo=github)

## 📖 About

Nalix is a real-time server solution designed for efficient communication and data sharing. It enables instant messaging, data synchronization, and secure networking, making it ideal for applications requiring live updates.

## 🛠️ Latest Builds

| Environment | Status |
|-------------|--------|
|![linux](https://badgen.net/badge/icon/Ubuntu%20Linux%2022.04%20x64?icon=terminal&label&color=orange)|[![Nalix](https://github.com/ppn-systems/Nalix/actions/workflows/ci-linux.yml/badge.svg?event=push)](https://github.com/ppn-systems/Nalix/actions/workflows/ci-linux.yml)|
|![win](https://badgen.net/badge/icon/Windows,.NET%2010?icon=windows&label&list=1)|[![Nalix](https://github.com/ppn-systems/Nalix/actions/workflows/ci-windows.yml/badge.svg?event=push)](https://github.com/ppn-systems/Nalix/actions/workflows/ci-windows.yml)|

## ✨ Features

- 🖥️ **Cross-Platform** – Runs on Windows, Linux, and macOS with .NET 10+.
- 🔄 **Real-time communication** – Supports instant messaging and state synchronization.
- 🔌 **Pluggable Protocols** – Easily add and swap network, serialization, or security protocols without modifying core logic.
- 🛤️ **Custom Middleware** – Define middleware to control authentication, validation, transformation, throttling, and more.
- ⚡ **High performance** – Designed to handle thousands of concurrent connections.
- 🔐 **Security-focused** – Implements encryption (ChaCha20-Poly1305, Salsa20-Poly1305) to protect data.
- 🛠️ **Extensible** – Easily customizable with your own protocols and handlers.
- 📡 **Live Updates** – Stay up to date with real-time updates, ensuring dynamic and responsive experiences.
- 💻 **Modern C# Implementation** – Leveraging cutting-edge C# features for clean, efficient, and maintainable code.
- 🧩 **SOLID & DDD Principles** – Adhering to SOLID principles and Domain-Driven Design for a robust and scalable architecture.

## 🔧 Requirements

- .NET 10 and C# 14 support
- Install .NET SDK 10 from [dotnet.microsoft.com](https://dotnet.microsoft.com/)
- `Visual Studio 2026` [**Download Visual Studio**](https://visualstudio.microsoft.com/downloads/)

## 💻 Technologies

- C#
- .Net 10
- Console Debug Logging
- XUnit Testing
- BenchmarkDotNet

    [![Technologies](https://skillicons.dev/icons?i=dotnet,cs,docker,git)](https://skillicons.dev)

## 📈 Benchmarks

> **Note:** All benchmarks are performed on **.NET 10.0**, **Intel i7-13620H**, **Windows 11**, using **BenchmarkDotNet v0.15.8**.

### 🔒 Envelope Encryption

| **Method**              | **Payload** | **Algorithm**         | **Mean**    | **Allocated** |
|-------------------------|:-----------:|:---------------------:|------------:|--------------:|
| Encrypt                 |    128      | SALSA20               |   356 ns    |    -          |
| Decrypt                 |    128      | SALSA20               |   281 ns    |    48 B       |
| Encrypt                 |   8192      | CHACHA20_POLY1305     | 48,649 ns   |    -          |
| Decrypt                 |   8192      | CHACHA20_POLY1305     | 26,153 ns   |    48 B       |

---

### 🏎️ X25519 ECC

| **Method**                                     | **KeyPairCount** | **Mean**   | **Allocated** |
|------------------------------------------------|:----------------:|-----------:|--------------:|
| X25519.GenerateKeyPair (CSPRNG + scalar mult)  |        1         | 65.36 μs   |   112 B       |
| X25519.GenerateKeyFromPrivateKey (scalar only) |        1         | 67.35 μs   |   112 B       |
| X25519.Agreement (shared secret)               |        1         | 66.59 μs   |    56 B       |

---

### 🔄 Serialization

| **Method**                                                | **ArrayLength** | **Mean (ns)** | **Allocated** |
|-----------------------------------------------------------|----------------:|--------------:|--------------:|
| Serialize<`int`[]> ➔ byte[]                               |       256       |    0.0476     |      -        |
| Deserialize<`int`> <- ReadOnlySpan<`byte`> (ref)          |       256       |    0.1097     |      -        |
| Serialize<`LargeStruct`> ➔ existing `byte`[] buffer       |      2048       |    0.0396     |      -        |
| Deserialize<`LargeStruct`> <- ReadOnlySpan<`byte`> (ref)  |      2048       |    0.2274     |     1 B       |

... *See more in the detailed benchmark report file.*

---

> **More details:** See the `docs/Nalix.Benchmarks` folder in the repository for full data and additional test cases.

---

## 📦 Available NuGet Packages

| Package ID         |Description                                                                                                        |
|--------------------|-------------------------------------------------------------------------------------------------------------------|
| **Nalix.SDK**      | Client-side SDK offering controllers, time sync, and localization utilities for connecting to Nalix.Network.      |
| **Nalix.Common**   | Core abstractions, enums, and shared contracts for the Nalix ecosystem.                                           |
| **Nalix.Logging**  | Asynchronous and high-performance logging subsystem with batching and multiple sinks.                             |
| **Nalix.Network**  | Core networking runtime providing TCP/UDP connections, protocol pipelines, and throttling.                        |
| **Nalix.Framework**| High-level framework providing identity, injection, randomization, and task orchestration.                        |

### 📦 Installation

You can install Nalix packages individually via NuGet:

```bash
dotnet add package Nalix.SDK
dotnet add package Nalix.Common
dotnet add package Nalix.Logging
dotnet add package Nalix.Network
dotnet add package Nalix.Framework
```

All Nalix packages target .NET 10 with full support for C# 14 features.

## 🛠️ Contributing

When contributing, please read [CONTRIBUTING.md](CONTRIBUTING.md) for the development workflow, commit convention, and pull request guidelines. Please also follow our [Code of Conduct](CODE_OF_CONDUCT.md) and submit PRs with proper documentation and tests.

## 🛡️ Security

Please review our [Security Policy](SECURITY.md) for supported versions and vulnerability reporting procedures.

## 📜 License

_Nalix is copyright &copy; PhcNguyen - Provided under the [Apache License, Version 2.0](http://apache.org/licenses/LICENSE-2.0.html)._

## 📬 Contact

For questions, suggestions, or support, open an issue on [GitHub](https://github.com/ppn-systems/Nalix/issues) or contact the maintainers at [ppn.system@gmail.com](mailto:ppn.system@gmail.com).

Give a ⭐️ if this project helped you!
