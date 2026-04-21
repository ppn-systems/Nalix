# Hosting Model

The Nalix Hosting Model is a high-level abstraction designed to feel familiar to .NET developers. It mirrors the design patterns of ASP.NET Core, providing a streamlined "fast-track" for building robust real-time servers.

## NetworkApplication

At the heart of the hosting model is the `NetworkApplication`. It is the entry point that orchestrates the lifetime of the server, managing listeners, DI containers, and the middleware pipeline.

### The Builder Pattern

You configure your application using the `NetworkApplicationBuilder`. This allows for a fluent, easy-to-read setup process:

```csharp
using Nalix.Network.Hosting;
using Nalix.Framework.Injection;
using Nalix.Runtime.Middleware;

var builder = NetworkApplication.CreateBuilder(args);

// 1. Configure Services (Instance Management)
InstanceManager.Instance.Register<IDatabase>(new MyMongoDatabase());

// 2. Configure Listeners
builder.ConfigureCertificate("path/to/certificate.private")
       .AddTcpListener(options => {
           options.Port = 8080;
       });

var app = builder.Build();

// 3. Configure Middleware Pipeline
app.UseMiddleware<AuthMiddleware>();

// 4. Map Handlers
app.MapHandlers(); // Automatically discovers [PacketController] classes

await app.RunAsync();
```

## Application Lifecycle

The hosting model manages the four major stages of a server's life:

1.**Bootstrap**: Loading configuration (appsettings.json), registering services in the DI container.
2.**Startup**: Opening TCP/UDP sockets, warming up object pools, and starting background worker loops.
3.**Runtime**: Accepting connections, dispatching packets, and managing session state.
4.**Shutdown**: Graceful disconnection of all clients, draining the packet queue, and closing listeners safely.

## Why use the Hosting Model?

While you can use the low-level `Nalix.Network` APIs directly, the Hosting model provides several built-in advantages:

- **Extreme Performance**: Uses the `InstanceManager` for zero-allocation service resolution.
- **Configuration**: Automatic binding to `IConfiguration` sources (JSON, Environment variables).
- **Discovery**: Auto-scanning for packet handlers and controllers, reducing manual boilerplate.
- **Logging**: Integrated logging via `ILogger` abstractions.

## Related Topics

- [Instance Manager](../../api/framework/runtime/instance-manager.md)
- [Middleware Pipeline](../runtime/middleware-pipeline.md)
- [Server Setup Guide](../../guides/getting-started/server-blueprint.md)
