---
title: "Hosting Bootstrap"
description: "Learn how Nalix bootstraps a server through NetworkApplicationBuilder, typed options, automatic packet discovery, and runtime initialization."
---

The hosting bootstrap concept is the opinionated server entry point in Nalix. It exists so you can describe a server in terms of packets, handlers, options, and protocols without manually wiring registries, dispatchers, listeners, hubs, or certificate initialization.

## What This Concept Is

The core types are `NetworkApplication`, `INetworkApplicationBuilder`, `NetworkApplicationBuilder`, `Bootstrap`, and `HostingOptions` in [src/Nalix.Network.Hosting](/workspace/home/nalix/src/Nalix.Network.Hosting). `NetworkApplication.CreateBuilder()` gives you the fluent server bootstrap surface, while `Bootstrap.Initialize()` configures server-side defaults such as `server.ini`, packet pooling, option templates, global exception handlers, and timer precision.

## How It Relates To Other Concepts

- It uses the [Packet Model](/workspace/home/codedocs-template/content/docs/packet-model.mdx) to build or accept an `IPacketRegistry`.
- It creates the [Dispatch Pipeline](/workspace/home/codedocs-template/content/docs/dispatch-pipeline.mdx) before listeners are activated.
- It hosts the transport layer that the [Transport Sessions](/workspace/home/codedocs-template/content/docs/transport-sessions.mdx) connect to.

## How It Works Internally

`NetworkApplicationBuilder` accumulates instructions in a hosting context. Methods such as `Configure<TOptions>(...)`, `AddPacket(...)`, `AddHandler(...)`, `AddMetadataProvider(...)`, `ConfigureDispatch(...)`, `AddTcp<TProtocol>(...)`, and `AddUdp<TProtocol>(...)` only record configuration. The real work happens inside `Build()` in [NetworkApplicationBuilder.cs](/workspace/home/nalix/src/Nalix.Network.Hosting/NetworkApplicationBuilder.cs).

At build time the builder creates:

- a `prepareCallbacks` action that registers the logger, applies typed options, creates the packet registry, and initializes the handshake certificate path
- a dispatch factory that creates `PacketDispatchChannel`
- server factories that build concrete TCP and UDP listeners only after dispatch exists
- a `NetworkApplication` that activates and deactivates everything in order

`NetworkApplication.ActivateAsync(...)` in [NetworkApplication.cs](/workspace/home/nalix/src/Nalix.Network.Hosting/NetworkApplication.cs) calls `prepareCallbacks`, creates the packet dispatch instance, registers it into `InstanceManager`, activates dispatch, starts listeners, and finally activates any hosted services. `DeactivateAsync(...)` reverses that order to stop listeners, dispose protocols, stop hosted services, and then stop the dispatcher.

`Bootstrap.Initialize()` in [Bootstrap.cs](/workspace/home/nalix/src/Nalix.Network.Hosting/Bootstrap.cs) is separate from the builder but tightly related. It switches the configuration file path to `server.ini`, enables packet pooling, materializes the core options, applies `HostingOptions`, flushes configuration defaults, and prints the startup banner unless the process is non-interactive or the banner is disabled.

```mermaid
flowchart TD
  A[Bootstrap.Initialize] --> B[server.ini defaults]
  C[CreateBuilder] --> D[Record options, packets, handlers, protocols]
  D --> E[Build()]
  E --> F[prepareCallbacks]
  F --> G[Register logger and options]
  G --> H[Create PacketRegistry]
  H --> I[Create PacketDispatchChannel]
  I --> J[Create listeners]
  J --> K[NetworkApplication.RunAsync]
```

## Basic Usage

```csharp
using Microsoft.Extensions.Logging;
using Nalix.Network.Hosting;
using Nalix.Network.Options;

ILogger logger = NLogix.Host.Instance;

using NetworkApplication app = NetworkApplication.CreateBuilder()
    .ConfigureLogging(logger)
    .Configure<NetworkSocketOptions>(options =>
    {
        options.Port = 57206;
        options.Backlog = 1024;
    })
    .AddPacket<MyPacket>()
    .AddHandler<MyHandlers>()
    .AddTcp<MyProtocol>()
    .Build();

await app.RunAsync();
```

That path is source-backed by `Build()` and `RunAsync(...)` in the hosting package.

## Advanced Usage

Use explicit infrastructure injection when you want control over pooling, connection storage, certificates, or packet discovery.

```csharp
using Microsoft.Extensions.Logging;
using Nalix.Framework.Memory.Buffers;
using Nalix.Network.Connections;
using Nalix.Network.Hosting;
using Nalix.Network.Options;
using Nalix.Runtime.Options;

ConnectionHub hub = new();
BufferPoolManager buffers = new();
ILogger logger = new NLogix(cfg =>
{
    cfg.SetMinimumLevel(LogLevel.Information)
       .RegisterTarget(new BatchConsoleLogTarget());
});

using NetworkApplication app = NetworkApplication.CreateBuilder()
    .ConfigureLogging(logger)
    .ConfigureConnectionHub(hub)
    .ConfigureBufferPoolManager(buffers)
    .ConfigureCertificate("./identity/certificate.private")
    .Configure<NetworkSocketOptions>(options =>
    {
        options.Port = 57206;
        options.BufferSize = 1024 * 64;
    })
    .Configure<DispatchOptions>(options =>
    {
        options.MaxPerConnectionQueue = 4096;
    })
    .AddPacketNamespace("MyApp.Contracts", recursive: true)
    .AddHandlers<MyHandlers>()
    .ConfigureDispatch(options =>
    {
        options.WithDispatchLoopCount(8)
               .WithLogging(logger);
    })
    .AddTcp<MyProtocol>()
    .Build();
```

## Common Configuration Types

These are the most important option classes that the hosting bootstrap touches directly:

| Option type | Source file | Purpose |
|---|---|---|
| `HostingOptions` | `src/Nalix.Network.Hosting/Options/HostingOptions.cs` | Startup banner, console behavior, ThreadPool floor, global exception hooks, timer precision |
| `NetworkSocketOptions` | `src/Nalix.Network/Options/NetworkSocketOptions.cs` | Port, backlog, buffer size, timeout, parallel accept loops |
| `ConnectionHubOptions` | `src/Nalix.Network/Options/ConnectionHubOptions.cs` | Hub capacity, sharding, broadcast behavior |
| `ConnectionLimitOptions` | `src/Nalix.Network/Options/ConnectionLimitOptions.cs` | Per-IP quotas, UDP datagram limits, error thresholds |
| `NetworkCallbackOptions` | `src/Nalix.Network/Options/NetworkCallbackOptions.cs` | Callback backpressure and per-IP fairness |
| `DispatchOptions` | `src/Nalix.Runtime/Options/DispatchOptions.cs` | Per-connection queue bounds and fairness weights |

<Callout type="warn">Do not assume `Configure<TOptions>(...)` is just a local override. `NetworkApplicationBuilder` uses `ConfigurationManager.Instance.Get<TOptions>()`, mutates the shared option instance, and then invokes a `Validate` method if it exists. If you are changing values in multiple places, remember you are editing shared global state, not a per-builder copy.</Callout>

<Accordions>
<Accordion title="Auto-discovery vs pre-built packet registries">
Auto-discovery through `AddPacket(...)`, `AddPacketNamespace(...)`, and `AddHandlers(...)` is the most ergonomic path and matches the intended design of the hosting layer. It keeps server startup concise and guarantees that handler assemblies are also considered during packet registry construction. The trade-off is startup reflection and discovery scope: if you are loading many assemblies or want very explicit control over what becomes a packet, a pre-built `IPacketRegistry` passed through `ConfigurePacketRegistry(...)` is easier to audit. Use pre-built registries in tightly controlled deployments or plugin systems where packet boundaries need to stay explicit.
</Accordion>
<Accordion title="Default infrastructure vs injected infrastructure">
If you do nothing, the builder will create a `ConnectionHub`, a `BufferPoolManager`, and a packet dispatcher for you. That makes the common path short and keeps sample code approachable. The trade-off is visibility and reuse: when you want a shared hub across listeners, custom session storage, or custom buffer pool reporting, you should inject the concrete infrastructure yourself with `ConfigureConnectionHub(...)` and `ConfigureBufferPoolManager(...)`. Explicit injection also makes tests easier because you control the exact instances that receive registrations and lifecycle calls.
</Accordion>
</Accordions>

## Source Files To Read

- [NetworkApplication.cs](/workspace/home/nalix/src/Nalix.Network.Hosting/NetworkApplication.cs)
- [INetworkApplicationBuilder.cs](/workspace/home/nalix/src/Nalix.Network.Hosting/INetworkApplicationBuilder.cs)
- [NetworkApplicationBuilder.cs](/workspace/home/nalix/src/Nalix.Network.Hosting/NetworkApplicationBuilder.cs)
- [Bootstrap.cs](/workspace/home/nalix/src/Nalix.Network.Hosting/Bootstrap.cs)
- [HostingOptions.cs](/workspace/home/nalix/src/Nalix.Network.Hosting/Options/HostingOptions.cs)
