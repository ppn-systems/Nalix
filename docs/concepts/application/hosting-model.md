# Hosting Model

The Nalix Hosting Model is a high-level abstraction designed to feel familiar to .NET developers. It provides a builder/build/run workflow around packet registration, dispatch configuration, and listener binding.

## NetworkApplication

At the heart of the hosting model is the `NetworkApplication`. It is the entry point that orchestrates the lifetime of the server, managing listeners, DI containers, and the middleware pipeline.

### The Builder Pattern

You configure your application using the `NetworkApplicationBuilder`. This allows for a fluent, easy-to-read setup process:

```csharp
using Nalix.Hosting;
using Nalix.Network.Options;

var builder = NetworkApplication.CreateBuilder();

builder.UseSecureConnections("path/to/certificate.private")
       .Configure<NetworkSocketOptions>(options => options.Port = 8080)
       .MapHandlers<MyHandlers>()
       .ListenTcp<MyProtocol>()
       .Bind();

var app = builder.Build();

await app.RunAsync();
```

## Application Lifecycle

The hosting model manages the four major stages of a server's life:

1. **Bootstrap**: Loading configuration, registering logger / pools / packet registry, and initializing handshake identity.
2. **Startup**: Creating the packet dispatcher and opening TCP or UDP listeners.
3. **Runtime**: Accepting connections, dispatching packets, and managing session state.
4. **Shutdown**: Stopping listeners and disposing runtime resources through `NetworkApplication`.

During `Build()`, `NetworkApplicationBuilder` applies every registered `Configure<TOptions>(...)`, ensures an `IConnectionHub` and a buffer pool manager exist (creating defaults if you didn't call `UseConnectionHub`/`UseBufferPoolManager`), initializes handshake handling, then creates the dispatcher and listeners — in that order.

## Why use the Hosting Model?

While you can use the low-level `Nalix.Network` APIs directly, the Hosting model provides several built-in advantages:

- **Fluent Setup**: Listener binding, packet discovery, and dispatch configuration live in one builder.
- **Configuration**: Strongly-typed options are applied through `Configure<TOptions>(...)`.
- **Explicit Registration**: Handlers are registered with `MapHandlers<THandler>()` for source-generated dispatch.
- **Logging**: Integrated logging via `ILogger` abstractions.

## Related Topics

- [Instance Manager](../../api/framework/instance-manager.md)
- [Middleware Pipeline](../internals/middleware-pipeline.md)
- [Server Setup Guide](../../guides/getting-started/server-blueprint.md)
