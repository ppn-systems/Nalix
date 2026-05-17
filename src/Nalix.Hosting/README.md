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

## Advanced Example: Enterprise Server Bootstrap

For production environments, `NetworkApplicationBuilder` integrates deeply with Microsoft Dependency Injection, logging frameworks, and custom configuration options:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nalix.Hosting;
using Nalix.Network.Options;

// Initialize the builder
var builder = NetworkApplication.CreateBuilder();

// 1. Bind TCP listeners and assign ports
builder.BindTcp<DefaultProtocol>()
    .OnPort(57200)
    .Bind();

// 2. Configure network socket options via builder API
builder.Configure<NetworkSocketOptions>(options =>
{
    options.NoDelay = true;             // Disable Nagle's algorithm for low-latency
    options.MaxParallel = 8;            // High concurrency acceptor loops
    options.BufferSize = 65536;         // Socket buffer size (64KB)
});

// 3. Register custom application services in the DI container
builder.Services.AddSingleton<IMyDatabase, MyDatabase>();

// 4. Discover custom PacketController types in the assembly
builder.ScanHandlers<Program>();

// Build and run the server application host
using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Server bootstrap completed successfully!");

await host.RunAsync();
```

## Documentation

For full end-to-end setup guides, check the [Quickstart](https://ppn-system.me/quickstart).
