# Installation

This page explains how to select the right Nalix packages for your project and verify that your environment meets the prerequisites.

## Prerequisites

| Requirement | Minimum |
|---|---|
| **.NET SDK** | 10.0 or later ([download](https://dotnet.microsoft.com/download)) |
| **C# language version** | 14 (default with .NET 10) |
| **IDE** | Visual Studio 2026, JetBrains Rider 2025.3+, or VS Code with C# Dev Kit |

## Choose Your Package Set

Install only the packages required for your role. Every package is available on [NuGet](https://www.nuget.org/packages?q=Nalix).

### Server (hosted — recommended)

The hosted server model provides a fluent builder and managed lifecycle. This is the recommended starting point for new projects.

```bash
dotnet add package Nalix.Hosting
dotnet add package Nalix.Logging
```

`Nalix.Hosting` transitively references `Nalix.Network`, `Nalix.Runtime`, `Nalix.Framework`, `Nalix.Codec`, `Nalix.Environment`, and `Nalix.Abstractions`.

### Server (manual wiring)

If you need full control over startup order without the hosting builder:

```bash
dotnet add package Nalix.Network
dotnet add package Nalix.Runtime
dotnet add package Nalix.Framework
dotnet add package Nalix.Abstractions
dotnet add package Nalix.Logging
```

### Client

```bash
dotnet add package Nalix.SDK
```

`Nalix.SDK` transitively references `Nalix.Codec`, `Nalix.Environment`, and `Nalix.Abstractions`.

### Shared contracts

If your packet definitions live in a separate assembly:

```bash
dotnet add package Nalix.Abstractions
dotnet add package Nalix.Codec
```

### Summary

| Scenario | Packages |
|---|---|
| Hosted server | `Nalix.Hosting`, `Nalix.Logging` |
| Manual server | `Nalix.Network`, `Nalix.Runtime`, `Nalix.Framework`, `Nalix.Abstractions`, `Nalix.Logging` |
| Client | `Nalix.SDK` |
| Shared contracts | `Nalix.Abstractions`, `Nalix.Codec` |
| Full stack | Server set + Client set, sharing one contracts assembly |

## Configuration File

Most server setups and many SDK examples load options from the `server.ini` file via `ConfigurationManager`. This file will be automatically generated in your project's output directory:

```ini
[NetworkSocketOptions]
Port=57206
Backlog=512

[DispatchOptions]
MaxPerConnectionQueue=4096
DropPolicy=DropNewest
BlockTimeout=00:00:01

[TransportOptions]
Address=127.0.0.1
Port=57206
ConnectTimeoutMillis=5000
BufferSize=65536
```

!!! note "Dispatch loop scaling"
    Worker-loop count is configured on `PacketDispatchOptions<TPacket>` in code via `WithDispatchLoopCount(...)`.
    Use `WithDispatchLoopCount(null)` to keep auto-scaling behavior.
    Note: `PacketDispatchOptions<TPacket>` (handler/middleware config) is a different type from `DispatchOptions` (queue bounds and drop policy).

## Validate Options at Startup

Validate options before opening sockets or creating sessions. Invalid configuration is cheaper to catch during startup than during live traffic.

```csharp
using Nalix.SDK.Options;
using Nalix.Network.Options;
using Nalix.Environment.Configuration;

// Server
NetworkSocketOptions socket = ConfigurationManager.Instance.Get<NetworkSocketOptions>();
socket.Validate();

// Client
TransportOptions transport = ConfigurationManager.Instance.Get<TransportOptions>();
```

## Package Dependency Graph

```mermaid
flowchart TD
    subgraph App ["Application Layer"]
        direction TB
        Hosting["Nalix.Hosting"]
        SDK["Nalix.SDK"]
    end

    subgraph Svc ["Service Layer"]
        direction TB
        Network["Nalix.Network"]
        Runtime["Nalix.Runtime"]
        Logging["Nalix.Logging"]
    end

    subgraph Core ["Core Layer"]
        direction TB
        Codec["Nalix.Codec"]
        Framework["Nalix.Framework"]
    end

    subgraph Base ["Base Layer"]
        direction TB
        Env["Nalix.Environment"]
        Abstractions["Nalix.Abstractions"]
    end

    Hosting --> Network
    Hosting --> Runtime
    Hosting --> Framework
    Hosting --> Codec
    
    SDK --> Codec

    Network --> Framework
    Runtime --> Codec
    Runtime --> Framework
    Logging --> Framework

    Codec --> Env
    Framework --> Env
    Env --> Abstractions
```

## What to Read Next

- [Introduction](./introduction.md) — Design philosophy and mental model
- [Quickstart](./quickstart.md) — Build your first Ping/Pong service
- [Packages Overview](./packages/index.md) — What each package provides
