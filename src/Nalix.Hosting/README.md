# Nalix.Hosting

> Fluent Microsoft-style builder API to bootstrap and host Nalix network servers with minimal boilerplate.

**Nalix.Hosting** provides a clean, familiar builder pattern (inspired by `WebApplicationBuilder`) for quickly creating high-performance TCP and UDP servers using the Nalix ecosystem.

## Features

- **Fluent & Intuitive Builder API**
- **`DefaultProtocol`** – Zero-boilerplate solution that forwards all packets to the dispatch pipeline
- **Automatic Handler Discovery** via `ScanHandlers`
- **Full TCP + UDP Support** – Bind multiple listeners easily
- **Deep Integration** with `Microsoft.Extensions.Logging`, `InstanceManager`, and Configuration system
- **Robust Lifecycle Management** – `ActivateAsync` / `DeactivateAsync` / `RunAsync` with graceful shutdown
- **Smart Bootstrap** – Automatic `server.ini` loading, GC tuning, high-precision timer, diagnostic bridging, etc.

## Installation

```bash
dotnet add package Nalix.Network.Hosting
```

## Quick Example

Using DefaultProtocol (Recommended for most cases).

```csharp
using Nalix.Hosting;

using var app = NetworkApplication.CreateBuilder()
    .BindTcp<DefaultProtocol>()
        .OnPort(8080)
        .Bind()
    .ScanHandlers<Program>()           // Scan all PacketController in the assembly
    .Build();

await app.RunAsync();
```

For simple scenarios, use the built-in `DefaultProtocol` instead of creating your own:

```csharp
using var app = NetworkApplication.CreateBuilder()
    .BindTcp<DefaultProtocol>().OnPort(8080).Bind()
    .ScanPackets<MyPacket>()
    .ScanHandlers<MyHandlers>()
    .Build();
```

## Documentation

For full end-to-end setup guides, check the [Quickstart](https://ppn-system.me/quickstart).
