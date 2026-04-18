# Server Blueprint

!!! info "Learning Signals"
    - :fontawesome-solid-layer-group: **Level**: Intermediate
    - :fontawesome-solid-clock: **Time**: 15–20 minutes
    - :fontawesome-solid-book: **Prerequisites**: [Quickstart](../../quickstart.md)

This page provides the recommended architectural blueprint for a production-grade Nalix server. It moves beyond the single-file quickstart to a shape that scales as features, security policies, and diagnostic needs grow.

---

## 🏗️ Startup Architecture

A robust server follows a deterministic sequence:

```mermaid
flowchart TD
    A["Load Configuration"] --> B["Register Services"]
    B --> C["Compile Dispatch"]
    C --> D["Layer Transport"]
    D --> E["Diagnostics Boot"]
    
    subgraph Registry ["InstanceManager"]
        B
    end
    
    subgraph Pipeline ["Runtime Pipeline"]
        C
        D
    end
```

!!! success "Why this blueprint?"
    Treating the server startup as a sequence of discrete layers ensures that when the socket starts accepting traffic, every security policy and reporting hook is already "warm" and ready.

---

## 📁 Recommended Directory Structure

Consistency is key for maintainability. We recommend the following layout for a Nalix server project:

```text
📂 Server/
├── 📂 Bootstrap/           # Service & Dispatch wiring
├── 📂 Protocols/           # Transport protocol definitions
├── 📂 Handlers/            # Application logic (Controllers)
├── 📂 Middleware/          # Security & Policy filters
├── 📂 Metadata/            # Custom convention providers
└── 📂 Hosting/             # Entry point & Lifecycle management
```

---

## 🚀 The Blueprint Steps

### 1. Configuration & Validation
Load and validate focused network options before starting the runtime. Fail-fast is better than a runtime error in a worker loop.

```csharp
var socket = ConfigurationManager.Instance.Get<NetworkSocketOptions>();
socket.Validate();

var dispatchOptions = ConfigurationManager.Instance.Get<DispatchOptions>();
dispatchOptions.Validate();

var connectionLimits = ConfigurationManager.Instance.Get<ConnectionLimitOptions>();
connectionLimits.Validate();
```

### 2. Registry Initialization
The `InstanceManager` serves as the runtime core for shared infrastructure.

```csharp
using Microsoft.Extensions.Logging;
using Nalix.Logging;

ILogger logger = NLogix.Host.Instance;

InstanceManager.Instance.Register<ILogger>(logger);
InstanceManager.Instance.Register<IPacketRegistry>(packetRegistry);
```

### 3. Dispatch & Middleware Setup
Define your application pipeline in a centralized location.

```csharp
PacketDispatchChannel dispatch = new(options =>
{
    options.WithLogging(logger)
           .WithErrorHandling((ex, opcode) => logger.Error($"dispatch 0x{opcode:X4}", ex))
           .WithMiddleware(new AuthMiddleware())
           .WithMiddleware(new AuditMiddleware())
           .WithHandler(() => new AccountHandlers())
           .WithHandler(() => new MatchHandlers());
});
```

!!! tip "Centralized Wiring"
    Keep all `WithMiddleware` and `WithHandler` calls in a single bootstrap class. Spreading these across the codebase makes startup order nearly impossible to debug.

### 4. Protocol Implementation
Keep your protocol thin. It should strictly act as the bridge between raw frames and clean messages.

```csharp
public sealed class ServerProtocol : Protocol
{
    private readonly PacketDispatchChannel _dispatch;

    public ServerProtocol(PacketDispatchChannel dispatch)
    {
        _dispatch = dispatch;
        this.SetConnectionAcceptance(true);
    }

    public override void ProcessMessage(object? sender, IConnectEventArgs args)
        => _dispatch.HandlePacket(args.Lease, args.Connection);
}
```

---

## ⚡ Lifecycle Management

Managing the **Activation** and **Shutdown** order is critical for preventing connection "dangling."

| phase | Action | Detail |
|---|---|---|
| **Startup** | `dispatch.Activate()` | Warm up worker pools and middleware. |
| **Startup** | `listener.Activate()` | Open the socket and begin accepting. |
| **Shutdown**| `listener.Deactivate()`| Stop accepting; finish current frames. |
| **Shutdown**| `dispatch.Dispose()` | Cleanly terminate workers and middleware. |

---

## 📊 Diagnostics Surface

A production-ready blueprint always includes a way to query the internal health.

- `listener.GenerateReport()` — Socket backpressure and accept counts.
- `protocol.GenerateReport()` — Decryption/Decompression success rates.
- `dispatch.GenerateReport()` — Queue lengths and worker latency.

!!! info "Pro-Tip"
    Even if you don't have an Admin API, ensure your logs occasionally output these reports during periods of high traffic.

---

## Recommended Next Pages

- [Production Checklist](../deployment/production-checklist.md) { .md-button }
- [Custom Middleware](../extensibility/custom-middleware.md) { .md-button }
- [TCP Request/Response](../networking/tcp-patterns.md) { .md-button }

