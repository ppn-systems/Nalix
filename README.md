# ![Icon](https://raw.githubusercontent.com/phcnguyen/Nalix/refs/heads/master/assets/Nalix.ico) **Nalix**

![GitHub License](https://img.shields.io/github/license/phcnguyen/Nalix)

Nalix is a real-time server solution designed for efficient communication and data sharing. It enables instant messaging, data synchronization, and secure networking, making it ideal for applications requiring live updates.

## ✨ Features

- 🔄 **Real-time communication** – Supports instant messaging and state synchronization.
- ⚡ **High performance** – Designed to handle thousands of concurrent connections.
- 🔐 **Security-focused** – Implements encryption (ChaCha20-Poly1305, XTEA) to protect data.
- 🛠️ **Extensible** – Easily customizable with your own protocols and handlers.

## 🔧 Requirements

- .NET 9 and C# 13 support
- Install .NET SDK 9 from [dotnet.microsoft.com](https://dotnet.microsoft.com/)
- `Visual Studio 2022` [**Download Visual Studio**](https://visualstudio.microsoft.com/downloads/)

## 📦 Available NuGet Packages

| Package ID                |Description                             | Install Command                            |
|---------------------------|----------------------------------------|--------------------------------------------|
| **Nalix**                 | Core real-time server & client library | `dotnet add package Nalix`                 |
| **Nalix.Common**          | Common utilities for Nalix             | `dotnet add package Nalix.Common`          |
| **Nalix.Cryptography**    | Secure cryptographic functions         | `dotnet add package Nalix.Cryptography`    |
| **Nalix.Logging**         | Logging utilities for Nalix            | `dotnet add package Nalix.Logging`         |
| **Nalix.Network**         | Low-level networking functionality     | `dotnet add package Nalix.Network`         |
| **Nalix.Network.Package** | Custom packet handling for Nalix       | `dotnet add package Nalix.Network.Package` |
| **Nalix.Network.Web**     | WebSocket support for Nalix            | `dotnet add package Nalix.Network.Web`     |
| **Nalix.Shared**          | Shared models and definitions          | `dotnet add package Nalix.Shared`          |
| **Nalix.Storage**         | Storage solutions for Nalix            | `dotnet add package Nalix.Storage`         |

## 🛠️ Contributing

When contributing, please follow our [Code of Conduct](CODE_OF_CONDUCT.md) and submit PRs with proper documentation and tests.

## 📜 License

_Nalix is copyright &copy; PhcNguyen - Provided under the [Apache License, Version 2.0](http://apache.org/licenses/LICENSE-2.0.html)._

## 📬 Contact

For questions, suggestions, or support, open an issue on [GitHub](https://github.com/phcnguyen/Nalix/issues) or contact the maintainers.
