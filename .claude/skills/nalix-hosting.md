# Nalix.Hosting

## Role

Microsoft-style host and builder APIs for bootstrapping Nalix server applications. Wires configuration, diagnostics, packet dispatch, middleware, and TCP/UDP listener lifecycle into a familiar `CreateBuilder().Build().RunAsync()` workflow.

**Dependencies:** `Nalix.Abstractions`, `Nalix.Framework`, `Nalix.Codec`, `Nalix.Runtime`, `Nalix.Network`

This is the **highest-level** project in the dependency graph. It consumes all other layers.

## Directory Structure

```
Nalix.Hosting/
├── Bootstrap.cs                    # Static bootstrap helpers and startup orchestration
├── DefaultProtocol.cs              # Default protocol implementation
├── INetworkApplicationBuilder.cs   # Builder interface (fluent API)
├── IProtocolBindingBuilder.cs      # Protocol binding builder
├── NetworkApplication.cs           # Application host (manages full lifecycle)
├── NetworkApplicationBuilder.cs    # Builder implementation
├── Internal/                       # Internal wiring helpers
├── Options/                        # Hosting configuration options
├── Resource.resx                   # Embedded string resources
└── Resource.Designer.cs            # Auto-generated resource accessor
```

## Key Components

### NetworkApplication

The main application host. Manages:
1. Configuration loading (INI → POCO binding via source-gen)
2. Service registration (InstanceManager)
3. Diagnostic event bridging (EventSource → ILogger)
4. Packet handler registration
5. Middleware pipeline assembly
6. TCP/UDP listener lifecycle (start/stop)

### Builder Pattern

```csharp
using var host = NetworkApplication.CreateBuilder()
    .BindTcp<MyProtocol>().Bind()
    .AddHandler<MyPacketHandler>()
    .Configure<NetworkSocketOptions>(opt => opt.Port = 8080)
    .Build();

await host.RunAsync();
```

- `INetworkApplicationBuilder` — Fluent builder for registering handlers, middleware, options.
- `IProtocolBindingBuilder` — Configures transport protocol (TCP/UDP) binding.
- `NetworkApplicationBuilder` — Concrete builder implementation.

### Diagnostic Bridge

Automatically bridges `DiagnosticListener` events from `Nalix.Environment` and `Nalix.Framework` into `ILogger` with configurable minimum log level.

### Resource Strings

Uses `.resx` embedded resources for error messages and status strings. `Resource.Designer.cs` is auto-generated — do NOT edit manually.

## Build Notes

- `InternalsVisibleTo` in Debug mode: `Nalix.Network.Tests`, `Nalix.Network.Benchmarks`.
- Uses `ResXFileCodeGenerator` for resource compilation.

## Anti-Patterns

- Do NOT manually wire services — use `NetworkApplicationBuilder`.
- Do NOT edit `Resource.Designer.cs` — edit `Resource.resx` and regenerate.
- Do NOT bypass the builder for listener lifecycle management.
- Do NOT add project references beyond the declared dependency graph.
