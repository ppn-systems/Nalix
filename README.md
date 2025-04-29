<div style="
    display: flex;
    align-items: center;
    gap: 15px;
    margin: 2rem 0;">
    <img src="https://raw.githubusercontent.com/phcnguyen/Notio/refs/heads/master/assets/Nalix.ico"
         alt="Nalix Icon"
         width="65"
         height="65"
         style="border-radius: 8px;"/>
    <div>
        <h1 style="
            font-size: 2.5rem;
            font-weight: 700;
            color: #007bff;
            margin: 0;
            padding: 0;
            letter-spacing: 1px;
            font-family: 'Segoe UI', Arial, sans-serif;">
            NALIX
        </h1>
        <div style="
            font-size: 1.1rem;
            color: #666;
            margin-top: 5px;
            font-style: italic;">
            Modern Real-Time Communication Solution
        </div>
    </div>
</div>

![NuGet Version](https://img.shields.io/nuget/v/Nalix?style=flat-square&logo=nuget)
![NuGet Downloads](https://img.shields.io/nuget/dt/Nalix?style=flat-square&logo=nuget)

![GitHub License](https://img.shields.io/github/license/phcnguyen/Nalix?style=flat-square)
![GitHub commit activity](https://img.shields.io/github/commit-activity/m/phcnguyen/Nalix?style=flat-square&logo=github)

## 📖 About

Nalix is a real-time server solution designed for efficient communication and data sharing. It enables instant messaging, data synchronization, and secure networking, making it ideal for applications requiring live updates.

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

    [![My Skills](https://skillicons.dev/icons?i=dotnet,cs,docker,git)](https://skillicons.dev)

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

Give a ⭐️ if this project helped you!
