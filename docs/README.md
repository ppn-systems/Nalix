# 🚀 Nalix Documentation

**Nalix** is a high-performance, modern .NET backend and networking library for building secure, scalable, maintainable, and robust distributed systems, applications, and protocol servers.

This documentation provides an overview, quick navigation, best practices, and direct links to all main module docs.

---

## 📚 Table of Contents

| Section                                        | Description                                          |
|------------------------------------------------|------------------------------------------------------|
| [About Nalix](#-about-nalix)                   | What is Nalix? Major architecture overview           |
| [Key Features](#-key-features)                 | Highlights and what makes Nalix powerful             |
| [Module Summary](#%EF%B8%8F-module-summary)    | Overview of major modules and their roles            |
| [Getting Started](#-getting-started)           | How to install and use Nalix in your .NET project    |
| [Detailed Module Docs](#-detailed-module-docs) | Links to module-level documentation                  |
| [Best Practices](#-best-practices)             | General tips for usage, maintainability, and security|
| [License](#%F0%9F%9B%A1%EF%B8%8F-license)      | License and further info                             |

---

## 🧩 About Nalix

Nalix is built for .NET developers who need high-performance, secure and modular foundational libraries (🪐) for:

- Backend and API servers
- Protocol gateways
- Message routing, serialization/deserialization
- Memory pooling, zero-copy pipelines
- Cryptography, secure randoms, object pools
- Distributed scheduling & configuration

It is designed on Domain-Driven Design (DDD) and SOLID principles for easy maintenance and scaling.  
Modules are neatly separated to keep dependencies clean and testing easy.

### Modern, Layered Architecture

```plaintext
Nalix.Shared   →   Nalix.Framework   →   Nalix.Network
   |                 |                    |
 Low-level        Service tools        Network stack,
 Memory, Crypto,  Logging, Tasks,      Protocol, Routing
 Serialization    DI, Config           Listeners, Pipeline
```

---

## ✨ Key Features

- ⚡ **Performance:** Zero-allocation APIs, spans, buffer pooling, lock-free data structures
- 🔒 **Security:** Built-in CSPRNG, AEAD encryption, attribute-based sensitive data protection
- 🧱 **Modularity:** Use only what you need and scale up by stacking modules
- 🛠️ **Maintainable:** Interfaces, dependency-injection, configuration management
- 📦 **Cross-platform:** Runs on .NET 10+, supports Windows, Linux, MacOS
- 🔬 **Observability:** Structured logging, live diagnostics, configuration hot-reload

---

## 🗂️ Module Summary

| Module                   | Purpose / Highlights                                              |
|--------------------------|-------------------------------------------------------------------|
| **Nalix.Logging**        | Fast, batch file/console logging, structured metadata, log levels |
| **Nalix.Network**        | TCP/UDP listeners, connection pooling, protocol stack, middleware |
| **Nalix.Shared**         | (De)serialization, zero-copy memory, buffer & object pools        |
| **Nalix.Framework**      | Configuration (INI), DI, time utilities, ID generators, task mgmt |
| `BufferLease`            | Pooled, sliceable, zero-copy buffer manager, secure clearing      |
| `ObjectPoolManager`      | Type-safe, concurrent, preallocatable object pooling              |
| `LiteSerializer`         | High-throughput binary serializer, supports struct/array/nullable |
| `PacketRegistry`         | Fast deserializer/transformer registry for protocol packets       |
| `EnvelopeCipher`         | Envelope AEAD/counter encryption with nonce/tag, best-practice    |
| `Csprng`                 | Cryptographically secure random for keys, tokens, IDs             |
| `Snowflake`              | Compact, distributed 56-bit sortable IDs (like Twitter Snowflake) |
| `TaskManager`            | Robust scheduled/background task, recurring/one-off workers       |

_Note: See the [Detailed Module Docs](#-detailed-module-docs) for deep dives and API examples._

---

## 🏁 Getting Started

### 1️⃣ Install via NuGet

```shell
dotnet add package Nalix.Shared
dotnet add package Nalix.Framework
dotnet add package Nalix.Network
dotnet add package Nalix.Logging
```

### 2️⃣ Import Namespaces & Use

```csharp
using Nalix.Logging;
using Nalix.Network.Middleware;
using Nalix.Shared.Memory.Buffers;

// Example: Start a logger
NLogix logger = NLogix.Host.Instance;
logger.Info("Welcome to Nalix!");
```

### 3️⃣ Explore Examples

Each module's documentation (linked below) includes code samples and usage scenarios.

---

## 📖 Detailed Module Docs

| Documentation                                                                 | Description                                   |
|-------------------------------------------------------------------------------|-----------------------------------------------|
| [Logging](./Nalix.Logging/README.md)                                          | Logging API, batch sinks, config              |
| [Middleware Pipeline](./Nalix.Network/Middleware/README.md)                   | Packet middleware, stages, attributes         |
| [Packet Dispatch & Handler](./Nalix.Network/Routing/PacketDispatchChannel.md) | Attribute-driven handler registration, routing|
| [Protocol](./Nalix.Network/Protocol/README.md)                                | Abstract protocol OnAccept, ProcessMessage    |
| [Connection & IConnection](./Nalix.Network/Connections/Connection.md)         | Socket connection, TCP/UDP transport, Secret, |
| [TCP/Connection](./Nalix.Network/Listeners/TcpListenerBase.md)                | Listener setup and connection lifecycle       |
| [ConnectionHub](./Nalix.Network/Connections/ConnectionHub.md)                 | Sharded, high-throughput connection manager   |
| [PacketContext](./Nalix.Network/Routing/PacketContext.md)                     | Handler context: Packet, Conn, Send, Attribute|
| [BufferLease](./Nalix.Shared/Memory/BufferLease.md)                           | Zero-copy buffer pool, secure                 |
| [ObjectPoolManager](./Nalix.Shared/Memory/ObjectPoolManager.md)               | Generic object pooling, preallocation         |
| [LiteSerializer](./Nalix.Shared/LiteSerializer.md)                            | Serialization of structs, arrays, primitives  |
| [PacketRegistry](./Nalix.Shared/PacketRegistry.md)                            | Typed packet registry and deserialization     |
| [EnvelopeCipher & Encryptor](./Nalix.Shared/Security/EnvelopeCipher.md)       | Envelope/AEAD encryption/decryption           |
| [CSPRNG](./Nalix.Framework/Csprng.md)                                         | Secure random numbers, nonces, integers       |
| [Snowflake ID](./Nalix.Framework/Snowflake.md)                                | Unique distributed ID generator (Snowflake)   |
| [Configuration](./Nalix.Framework/Configuration.md)                           | Thread-safe INI management, container binding |
| [TaskManager](./Nalix.Framework/TaskManager.md)                               | Background scheduling, recurring tasks        |
| [Full Architecture](./Nalix.Network/Architecture.md)                          | Layered design, DDD/Clean, flows & diagrams   |

---

## 📝 Best Practices

- Use zero-copy APIs (`Span<T>`, `BufferLease`) for performance-critical code
- Always securely clear secrets (see `BufferLease` & `EnvelopeCipher`)
- Design configuration classes by inheriting `ConfiguredBinder` and use `ConfiguredShared`
- Attribute-annotate fields for serialization, encryption, and logging
- Favor registering/discovering packet handlers using `[PacketController]` & `[PacketOpcode]` attributes
- Leverage dependency injection patterns for testable, robust code
- Call `Flush()` and `Dispose()` on configuration and pool classes for clean shutdown
- Follow Microsoft XML doc standards in comments for public APIs

---

## 🛡️ License

Nalix is released under the [Apache License, Version 2.0](https://www.apache.org/licenses/LICENSE-2.0).  
See LICENSE files in the repo and modules for details.

---

> For feature requests, contribution guidelines, and more documentation, visit the repository or reach out to the Nalix team.
