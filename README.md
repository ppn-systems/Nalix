# ![Icon](https://raw.githubusercontent.com/phcnguyen/Notio/refs/heads/master/assets/Notio.ico) **Notio**

![GitHub License](https://img.shields.io/github/license/phcnguyen/Notio)

Notio is a real-time server solution designed to facilitate efficient communication and data sharing. It allows users to exchange messages and information quickly, providing a robust backend for various applications requiring live updates and instant data synchronization.

## Features

- **Real-time communication**: Supports instant messaging and data synchronization.
- **Scalability**: Designed to handle a large number of concurrent connections.
- **Security**: Implements encryption and other security measures to protect data.
- **Extensibility**: Easily extendable with custom protocols and handlers.

## Requirements

- `Visual Studio 2022` (.NET 9 and C# 13 support required)
- [**Download Visual Studio**](https://visualstudio.microsoft.com/downloads/)

## Source Code

Getting started with Notio couldn't be easier. Make sure you have Visual Studio 2022 Community installed.

- Clone the source: `git clone https://github.com/phcnguyen/Notio.git`
- Open `Notio.sln`
- Restore Nuget packages
- Build

## Nuget Packages

| ID | Package Name              | Command                                             |
|----|---------------------------|-----------------------------------------------------|
| 1  | **Notio**                 | `dotnet add package Notio`                          |
| 2  | **Notio.Common**          | `dotnet add package Notio.Common`                   |
| 3  | **Notio.Cryptography**    | `dotnet add package Notio.Cryptography`             |
| 4  | **Notio.Logging**         | `dotnet add package Notio.Logging`                  |
| 5  | **Notio.Network**         | `dotnet add package Notio.Network`                  |
| 6  | **Notio.Network.Package** | `dotnet add package Notio.Network.Package`          |
| 7  | **Notio.Network.Web**     | `dotnet add package Notio.Network.Web`              |
| 8  | **Notio.Serialization**   | `dotnet add package Notio.Serialization`            |
| 9  | **Notio.Shared**          | `dotnet add package Notio.Shared`                   |
| 10 | **Notio.Storage**         | `dotnet add package Notio.Storage`                  |

### Contributing

When contributing please keep in mind our [Code of Conduct](CODE_OF_CONDUCT.md).

_Notio is copyright &copy; PhcNguyen - Provided under the [Apache License, Version 2.0](http://apache.org/licenses/LICENSE-2.0.html)._

## Contact

For questions, suggestions, or support, please open an issue on GitHub or contact the maintainers directly.
