# Nalix.Hosting

> Fluent Microsoft-style builder API to bootstrap and host Nalix network servers with minimal boilerplate.

**Nalix.Hosting** provides a clean, familiar builder pattern (inspired by `WebApplicationBuilder`) for quickly creating high-performance TCP and UDP servers using the Nalix ecosystem.

## Features

- **Fluent & Intuitive Builder API**
- **`DefaultProtocol`** – Zero-boilerplate solution that forwards all packets to the dispatch pipeline
- **Explicit Handler Registration** via `MapHandlers<T>()` (AOT-safe, no assembly scanning)
- **Full TCP + UDP Support** – Bind multiple listeners easily
- **Deep Integration** with `Microsoft.Extensions.Logging`, `InstanceManager`, and Configuration system
- **Robust Lifecycle Management** – `ActivateAsync` / `DeactivateAsync` / `RunAsync` with graceful shutdown
- **Smart Bootstrap** – Automatic `server.ini` loading, GC tuning, high-precision timer, diagnostic bridging, etc.

## Installation

```bash
dotnet add package Nalix.Hosting
```

## Quick Example

Using DefaultProtocol (Recommended for most cases).

```csharp
using Nalix.Hosting;

using var app = NetworkApplication.CreateBuilder()
    .ListenTcp<DefaultProtocol>()
        .OnPort(8080)
        .Bind()
    .MapHandlers<MyPacketHandler>()  // Register handlers explicitly (AOT-safe)
    .Build();

await app.RunAsync();
```

## Advanced Example: Enterprise Server Bootstrap

For production environments, `NetworkApplicationBuilder` integrates deeply with system logger frameworks, custom configuration options, and the native Nalix `InstanceManager` container:

```csharp
using Microsoft.Extensions.Logging;
using Nalix.Hosting;
using Nalix.Network.Options;
using Nalix.Framework.Injection;

// Initialize the builder
var builder = NetworkApplication.CreateBuilder();

// 1. Bind TCP listeners and assign ports
builder.ListenTcp<DefaultProtocol>()
    .OnPort(57200)
    .Bind();

// 2. Configure network socket options via builder API
builder.Configure<NetworkSocketOptions>(options =>
{
    options.NoDelay = true;             // Disable Nagle's algorithm for low-latency
    options.MaxParallel = 8;            // High concurrency acceptor loops
    options.BufferSize = 65536;         // Socket buffer size (64KB)
});

// 3. Register custom application services in the InstanceManager container
InstanceManager.Instance.Register<IMyDatabase>(new MyDatabase());

// 4. Register custom PacketHandler handlers explicitly (AOT-safe)
builder.MapHandlers<MyPacketHandler>();

// Build and run the server application host
using var host = builder.Build();

var logger = InstanceManager.Instance.GetOrCreateInstance<ILogger>();
logger.LogInformation("Server bootstrap completed successfully!");

await host.RunAsync();
```

## Key Namespaces

| Namespace | Purpose | Key Types |
| :--- | :--- | :--- |
| `Nalix.Hosting` | Microsoft-style fluent host and builder APIs for quick bootstrapping | `NetworkApplication`, `NetworkApplicationBuilder`, `INetworkApplicationBuilder`, `DefaultProtocol` |
| `Nalix.Hosting.Internal` | Lifecycle event listeners, telemetry diagnostics, and logging bridges | `DiagnosticListenerFactory`, `HostingBuilderContext` |
| `Nalix.Hosting.Options` | Core execution and host bootstrapping configuration options | `HostEnvironmentOptions` |

## Documentation

For full end-to-end setup guides, check the [Quickstart](https://ppn.io.vn/quickstart).

