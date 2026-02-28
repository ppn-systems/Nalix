# Nalix.Network.Hosting

A fluent orchestration layer to build and host Nalix applications with minimal boilerplate.

## Features

- **NetworkApplicationBuilder**: Fluent API to configure listeners, protocols, and handlers.
- **Service Integration**: Built-in support for Microsoft.Extensions.Logging and InstanceManager.
- **Auto-Discovery**: Automatic scanning and registration of packet contracts and controllers.
- **Lifecycle Management**: Clean startup and shutdown orchestration for complex networking stacks.

## Installation

```bash
dotnet add package Nalix.Network.Hosting
```

## Quick Example: Modular Startup

```csharp
using var app = NetworkApplication.CreateBuilder()
    .AddTcp<MyProtocol>(port: 8080)
    .AddPacket<MyPacket>()
    .AddHandlers<MyHandlers>()
    .Build();

await app.RunAsync();
```

## Documentation

For full end-to-end setup guides, check the [Quickstart](https://ppn-systems.me/quickstart).
