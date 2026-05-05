# Nalix.Network.Hosting

> Fluent orchestration layer to build and host Nalix applications with minimal boilerplate.

## Key Features

| Feature | Description |
| :--- | :--- |
| 🏗️ **NetworkApplicationBuilder** | Fluent API to configure listeners, protocols, and handlers. |
| 🔌 **Service Integration** | Built-in support for `Microsoft.Extensions.Logging` and `InstanceManager`. |
| 🔍 **Auto-Discovery** | Automatic scanning and registration of packet contracts and controllers. |
| ♻️ **Lifecycle Management** | Clean startup and shutdown orchestration for complex networking stacks. |

## Installation

```bash
dotnet add package Nalix.Network.Hosting
```

## Quick Example

```csharp
using Nalix.Network.Hosting;

using var app = NetworkApplication.CreateBuilder()
    .BindTcp<MyProtocol>().OnPort(8080).Bind()
    .ScanPackets<MyPacket>()
    .ScanHandlers<MyHandlers>()
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

For full end-to-end setup guides, check the [Quickstart](https://ppn-systems.me/quickstart).
