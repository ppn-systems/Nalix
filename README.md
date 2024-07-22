# ![Icon](https://raw.githubusercontent.com/ppn-systems/Nalix/refs/heads/master/assets/Nalix.ico) **Nalix**

![NuGet Version](https://img.shields.io/nuget/v/Nalix.Common?style=flat-square&logo=nuget)
![NuGet Downloads](https://img.shields.io/nuget/dt/Nalix.Common?style=flat-square&logo=nuget)

![GitHub License](https://img.shields.io/github/license/phcnguyen/Nalix?style=flat-square)
![GitHub commit activity](https://img.shields.io/github/commit-activity/m/phcnguyen/Nalix?style=flat-square&logo=github)

## 📖 About

Nalix is a real-time server solution designed for efficient communication and data sharing. It enables instant messaging, data synchronization, and secure networking, making it ideal for applications requiring live updates.

## 🛠️ Latest Builds

| Enviroment | Status |
|------------|--------|
|![linux](https://badgen.net/badge/icon/Ubuntu%20Linux%2022.04%20x64?icon=terminal&label&color=orange)|[![Nalix](https://github.com/phcnguyen/Nalix/actions/workflows/Linux.yml/badge.svg?event=push)](https://github.com/phcnguyen/Nalix/actions/workflows/Linux.yml)|
|![mac](https://badgen.net/badge/icon/macOS%20Latest?icon=apple&label&color=purple&list=1)|[![Nalix](https://github.com/phcnguyen/Nalix/actions/workflows/MacOs.yml/badge.svg?event=push)](https://github.com/phcnguyen/Nalix/actions/workflows/MacOs.yml)|
|![win](https://badgen.net/badge/icon/Windows,.NET%209?icon=windows&label&list=1)|[![Nalix](https://github.com/phcnguyen/Nalix/actions/workflows/Windows.yml/badge.svg?event=push)](https://github.com/phcnguyen/Nalix/actions/workflows/Windows.yml)|

## ✨ Features

- 🔄 **Real-time communication** – Supports instant messaging and state synchronization.
- ⚡ **High performance** – Designed to handle thousands of concurrent connections.
- 🔐 **Security-focused** – Implements encryption (ChaCha20-Poly1305, XTEA) to protect data.
- 🛠️ **Extensible** – Easily customizable with your own protocols and handlers.
- 📡 **Live Updates** – Stay up to date with real-time updates, ensuring dynamic and responsive experiences.
- 💻 **Modern C# Implementation** – Leveraging cutting-edge C# features for clean, efficient, and maintainable code.
- 🧩 **SOLID & DDD Principles** – Adhering to SOLID principles and Domain-Driven Design for a robust and scalable architecture.

## 🔧 Requirements

- .NET 9 and C# 13 support
- Install .NET SDK 9 from [dotnet.microsoft.com](https://dotnet.microsoft.com/)
- `Visual Studio 2022` [**Download Visual Studio**](https://visualstudio.microsoft.com/downloads/)

## 💻 Technologies

- C#
- .Net 9
- Console Debug Logging
- XUnit Testing

    [![Technologies](https://skillicons.dev/icons?i=dotnet,cs,docker,git)](https://skillicons.dev)

## 📦 Available NuGet Packages

| Package ID         |Description                             | Install Command                            |
|--------------------|----------------------------------------|--------------------------------------------|
| **Nalix.Common**   | Core abstractions, enums, and shared contracts for the Nalix ecosystem.| `dotnet add package Nalix.Common`|
| **Nalix.Framework**| High-level framework providing cryptography, identity, injection, randomization, and task orchestration.| `dotnet add package Nalix.Framework`|
| **Nalix.Logging**  | Asynchronous and high-performance logging subsystem with batching and multiple sinks.| `dotnet add package Nalix.Logging`|
| **Nalix.Network**  | Core networking runtime providing TCP/UDP connections, protocol pipelines, and throttling.| `dotnet add package Nalix.Network`|
| **Nalix.Shared**   | Shared low-level utilities and primitives such as memory pooling, LZ4 compression, and lightweight serialization.| `dotnet add package Nalix.Shared`          |
| **Nalix.SDK**      | Client-side SDK offering controllers, time sync, and localization utilities for connecting to Nalix.Network.| `dotnet add package Nalix.SDK`|

## 📦 Installation

You can install Nalix packages individually via NuGet:

```bash
dotnet add package Nalix.SDK
dotnet add package Nalix.Common
dotnet add package Nalix.Shared
dotnet add package Nalix.Logging
dotnet add package Nalix.Network
dotnet add package Nalix.Framework
```

All Nalix packages target .NET 8 and .NET 9 with full support for C# 13 features.

## 🛠️ Contributing

When contributing, please follow our [Code of Conduct](CODE_OF_CONDUCT.md) and submit PRs with proper documentation and tests.

## 🛡️ Security

Please review our [Security Policy](SECURITY.md) for supported versions and vulnerability reporting procedures.

## 📜 License

_Nalix is copyright &copy; PhcNguyen - Provided under the [Apache License, Version 2.0](http://apache.org/licenses/LICENSE-2.0.html)._

## 📬 Contact

For questions, suggestions, or support, open an issue on [GitHub](https://github.com/phcnguyen/Nalix/issues) or contact the maintainers.

Give a ⭐️ if this project helped you!
