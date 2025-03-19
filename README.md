# ![Icon](https://raw.githubusercontent.com/phcnguyen/Notio/refs/heads/master/assets/Notio.ico) **Notio**

![GitHub License](https://img.shields.io/github/license/phcnguyen/Notio)

Notio is a real-time server solution designed for efficient communication and data sharing. It enables instant messaging, data synchronization, and secure networking, making it ideal for applications requiring live updates.

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
| **Notio**                 | Core real-time server & client library | `dotnet add package Notio`                 |
| **Notio.Common**          | Common utilities for Notio             | `dotnet add package Notio.Common`          |
| **Notio.Cryptography**    | Secure cryptographic functions         | `dotnet add package Notio.Cryptography`    |
| **Notio.Logging**         | Logging utilities for Notio            | `dotnet add package Notio.Logging`         |
| **Notio.Network**         | Low-level networking functionality     | `dotnet add package Notio.Network`         |
| **Notio.Network.Package** | Custom packet handling for Notio       | `dotnet add package Notio.Network.Package` |
| **Notio.Network.Web**     | WebSocket support for Notio            | `dotnet add package Notio.Network.Web`     |
| **Notio.Shared**          | Shared models and definitions          | `dotnet add package Notio.Shared`          |
| **Notio.Storage**         | Storage solutions for Notio            | `dotnet add package Notio.Storage`         |

## 🛠️ Contributing

When contributing, please follow our [Code of Conduct](CODE_OF_CONDUCT.md) and submit PRs with proper documentation and tests.

## 📜 License

_Notio is copyright &copy; PhcNguyen - Provided under the [Apache License, Version 2.0](http://apache.org/licenses/LICENSE-2.0.html)._

## 📬 Contact

For questions, suggestions, or support, open an issue on [GitHub](https://github.com/phcnguyen/Notio/issues) or contact the maintainers.
