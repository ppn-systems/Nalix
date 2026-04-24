# 🏗️ Nalix AI Skill — Hosting & Application Bootstrapping

This skill covers the `Nalix.Network.Hosting` layer, which provides a high-level, fluent API for configuring and running Nalix servers.

---

## 🚀 The Builder Pattern

Nalix follows the Microsoft-style "Builder" pattern for application startup.

### `NetworkApplication.CreateBuilder()`
- **Purpose:** Initializes a new `NetworkApplicationBuilder`.
- **Default Services:** Automatically registers core Nalix services (Pooling, Dispatching, Logging).

### Configuration Chain:
```csharp
var builder = NetworkApplication.CreateBuilder();

builder.AddTcp<MyProtocol>()             // Transport
       .AddHandler<MyController>()       // Routing
       .AddMiddleware<RateLimiter>()     // Pipeline
       .Configure<SocketOptions>(...)    // Options
       .UseLogging();                    // Observability

using var app = builder.Build();
await app.RunAsync();
```

---

## 🛠️ Key Capabilities

- **DI Integration:** Nalix hosting integrates with `Microsoft.Extensions.DependencyInjection`.
- **Pluggable Protocols:** Easily swap between TCP and UDP or custom protocol implementations via `AddTcp<T>` / `AddUdp<T>`.
- **Metadata Providers:** Register `IPacketMetadataProvider` to augment packet metadata globally during the build phase.

---

## 🛤️ Host Lifecycle

1. **Build Phase:** Discover all controllers, compile handlers, and initialize the `PacketRegistry`.
2. **Start Phase:** Bind listeners (TCP/UDP sockets) and start the `ConnectionHub`.
3. **Execution Phase:** Handle connections and dispatch packets through the pipeline.
4. **Shutdown Phase:** Gracefully close connections, flush logs, and dispose of pooled resources.

---

## ⚡ Performance Mandates

- **Startup Pre-allocation:** Use `Configure<PoolingOptions>` to pre-allocate packet contexts and buffer slabs during the Build phase.
- **Thread Tuning:** Configure the `SocketOptions` to align the number of dispatch loops with the available CPU cores.

---

## 🛡️ Common Pitfalls

- **Missing Bindings:** Building an application without any `AddTcp` or `AddUdp` calls will result in an idle host (NALIX044).
- **Service Lifetimes:** Ensure that custom services registered in the DI container have appropriate lifetimes (Singleton vs. Scoped). Handlers are typically Singleton or transiently created from a pool.
- **Port Conflicts:** Ensure the configured port is not in use by another process.
