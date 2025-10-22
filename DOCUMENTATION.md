# Nalix Documentation

Welcome to the complete documentation for the Nalix ecosystem.

---

## Documentation Index

| Module | Description | Documentation |
|---------|--------------|----------------|
| **Nalix.Framework** | High-level framework providing cryptography, identity, injection, randomization, and task orchestration. | [Read →](./docs/Nalix.Framework/) |
| **Nalix.Network** | Core networking runtime (TCP/UDP, protocols, throttling, timing). | [Read →](./docs/Nalix.Network/) |
| **Nalix.Logging** | Asynchronous and high-performance logging system with batching and multiple sinks. | [Read →](./docs/Nalix.Logging/) |
| **Nalix.Shared** | Shared utilities for memory pooling, LZ4 compression, and lightweight serialization. | [Read →](./docs/Nalix.Shared/) |

---

## Overview

Nalix is a modular, real-time framework designed for efficient, secure, and scalable systems.

Each package is independently usable yet seamlessly integrated into the overall ecosystem.

---

## Quick Start

To install Nalix packages from NuGet:

```bash
dotnet add package Nalix.Framework
dotnet add package Nalix.Network
dotnet add package Nalix.Logging
```

For more examples, see [docs/README.md](./docs/README.md).

---

## Architecture Reference

For a deep dive into Nalix’s system architecture and design philosophy,  
see [docs/Architecture.md](./docs/Architecture.md).

---

## Additional Documents

- [README.md](./README.md) — Project overview and introduction  
- [SECURITY.md](./SECURITY.md) — Reporting vulnerabilities  
- [CODE_OF_CONDUCT.md](./CODE_OF_CONDUCT.md) — Contributor guidelines  
- [CHANGELOG.md](./CHANGELOG.md) — Version history and release notes  

---

> "Nalix empowers developers to build faster, safer, and smarter real-time systems."
