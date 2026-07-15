# Server Blueprint

This page shows a recommended shape for a production-grade Nalix server — the layout, startup order, and options you'll want once a server grows past the quickstart's single file.

---

## Recommended Directory Structure

```text
Server/
├── Bootstrap/           # Service & Dispatch wiring
├── Protocols/           # Transport protocol definitions
├── Handlers/            # Application logic (Controllers)
├── Middleware/          # Security & Policy filters
├── Metadata/            # Custom convention providers
└── Hosting/             # Entry point & Lifecycle management
```

---

## The Simplest Entry Point

For most applications, the hosting builder is all you need — see the basic `CreateBuilder()` chain in [Quick Start §3](../../quickstart.md#3-write-the-server). It wires configuration, dispatch, and listeners for you. The rest of this page adds the production concerns that sit around that chain.

`DefaultProtocol` (from `Nalix.Hosting.Protocols`) is a ready-made protocol that forwards every inbound packet straight to the dispatcher — you only need a custom protocol class if you have transport-level logic beyond that.

---

## Startup Steps

### 1. Configuration & Validation

Load and validate focused option types before starting the runtime. Fail-fast is better than a runtime error in a worker loop.

```csharp
var socket = ConfigurationManager.Instance.Get<NetworkSocketOptions>();
socket.Validate();

var dispatchOptions = ConfigurationManager.Instance.Get<DispatchOptions>();
dispatchOptions.Validate();

var connectionQuotas = ConfigurationManager.Instance.Get<ConnectionQuotaOptions>();
connectionQuotas.Validate();

var connectionGuard = ConfigurationManager.Instance.Get<ConnectionGuardOptions>();
connectionGuard.Validate();
```

If you use the hosting builder, `src/Nalix.Hosting/NetworkApplicationBuilder.cs` already does this for every `Configure<TOptions>(...)` registration by invoking `Validate()` when the option type exposes it.

### 2. Registry Initialization

The packet registry is built once at startup. This freezes the catalog of discovered packets (via source generators) and prepares it for high-performance deserialization. The hosting builder does this automatically during `app.Build()`; for manual composition you'd call it yourself:

```csharp
Nalix.Codec.DataFrames.PacketRegistry.Build();
```

### 3. Dispatch & Middleware Setup

Add middleware and handlers in a centralized location, using `ConfigureDispatchOptions` on the builder:

```csharp
builder.ConfigureDispatchOptions(options =>
{
    options
        .WithMiddleware(new RateLimitMiddleware())
        .WithErrorHandling((ex, opcode) =>
        {
            Console.WriteLine($"Error in opcode 0x{opcode:X4}: {ex.Message}");
        });
});
```

!!! tip "Centralized wiring"
    Keep all `WithMiddleware` and handler registration in one bootstrap location. Spreading it across the codebase makes startup order hard to debug.

### 4. Protocol Implementation

Keep a custom protocol thin — it should strictly bridge raw frames to the dispatcher. `DefaultProtocol` already does exactly this:

```csharp
// src/Nalix.Hosting/Protocols/DefaultProtocol.cs (shape)
public sealed class DefaultProtocol : Protocol
{
    private readonly IPacketDispatch _dispatch;

    public DefaultProtocol(ILogger logger, IPacketDispatch dispatch)
    {
        _dispatch = dispatch;
        this.IsAccepting = true;
    }

    public override void ProcessMessage(object? sender, IConnectionEventArgs args)
        => _dispatch.HandlePacket(args.Lease, args.Connection);
}
```

Full source: `src/Nalix.Hosting/Protocols/DefaultProtocol.cs`

---

## Lifecycle Management

Managing activation and shutdown order matters — it prevents connections from being left dangling.

| Phase | Action | Detail |
| --- | --- | --- |
| **Startup** | `dispatch.Activate()` | Warm up the dispatch pipeline before listeners begin accepting traffic. |
| **Startup** | `listener.Activate()` | Open the socket and begin accepting. |
| **Shutdown** | `listener.Deactivate()` | Stop accepting and release transport resources first. |
| **Shutdown** | `protocol.Dispose()` | Dispose protocols after listeners stop. |
| **Shutdown** | `dispatch.Deactivate()` | Stop the dispatch pipeline after listeners. |

This order comes directly from `src/Nalix.Hosting/NetworkApplication.cs`, where `ActivateAsync()` activates dispatch then starts listeners; `DeactivateAsync()` reverses that order and waits for the `"net/*"` and `"time/*"` task groups to finish.

The hosting builder's `RunAsync()` does all of this for you — `app.RunAsync(cts.Token)` calls `ActivateAsync()`, waits for cancellation, then calls `DeactivateAsync()`.

---

## Diagnostics Surface

All core components implement `IReportable`, giving you a way to inspect internal health without an admin API:

- `GenerateReport()` — a human-readable string for logging or CLI.
- `WriteReportData(Utf8JsonWriter)` — JSON output for monitoring APIs or dashboards.

Available on `IListener` (transport state and counters), `IProtocol` (protocol-side counters), and `IPacketDispatch` (dispatch state and channel pressure). Even without a dedicated admin endpoint, logging these reports periodically during high traffic is worth doing.

---

## Manual Composition (No Hosting Builder)

!!! warning "Advanced only"
    Use this path only if you need to bypass the hosting layer entirely — for example, to build a specialized transport library. Manual composition must wire the dispatch pipeline, protocol, and listener yourself, in the same order the hosting builder uses internally.

```csharp
PacketDispatchChannel dispatch = new(options =>
{
    options.WithHandler(() => new HelloHandlers());
});

dispatch.Activate();
// ... construct and activate your listener with `dispatch` wired in ...
```

See [Manual Wiring (No Hosting)](../../concepts/internals/minimal-server.md) for the full low-level walkthrough.

---

## Best Practices Checklist

- Define shared packet contracts in their own project, referenced by both server and client.
- Log through `ILogger`, not `Console.WriteLine`, once you leave the quickstart stage.
- Validate every options type at startup (see [Configuration & Validation](#1-configuration-validation)).
- Keep custom protocols thin — forward to the dispatcher, don't embed application logic in them.

## Recommended Next Pages

- [Production Checklist](../deployment/production-checklist.md) { .md-button }
- [Custom Middleware](../extensibility/custom-middleware.md) { .md-button }
- [TCP Request/Response](../networking/tcp-patterns.md) { .md-button }
