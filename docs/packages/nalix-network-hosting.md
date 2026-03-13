# Nalix.Network.Hosting

`Nalix.Network.Hosting` adds a Microsoft-style builder and application lifecycle on top of `Nalix.Network` and `Nalix.Runtime`.

Use it when you want Nalix server startup to feel more like a single bootstrap pipeline instead of manually wiring listeners, dispatchers, and protocols yourself.

!!! tip "Standard Entry Point"
    This package is the recommended way to build Nalix servers. It provides a familiar fluent API for configuring everything from logging and options to handlers and server bindings.

## Hosting Flow

```mermaid
flowchart LR
    subgraph Config ["Configuration"]
        A["CreateBuilder()"] --> B["Register Services"]
        B --> C["Scan Packets"]
        C --> D["Bind Protocols"]
    end

    subgraph Runtime ["Execution"]
        D --> E["Build & Activate"]
        E --> F["Infrastructure"]
        F --> G["Dispatcher"]
        G --> H["Listeners"]
    end
```

## What it gives you

- `NetworkApplication.CreateBuilder()`
- fluent `INetworkApplicationBuilder` configuration
- automatic packet registry creation from packet and handler assemblies
- packet metadata provider registration
- automatic UDP and TCP server listener management
- application lifecycle activation through `ActivateAsync`, `DeactivateAsync`, and `RunAsync`

## Builder notes

- `ConfigureConnectionHub(...)` takes an `IConnectionHub`; use it when you want to supply your own shared hub instead of the default fallback instance.
- `ConfigureBufferPoolManager(...)` is the explicit way to supply a custom `BufferPoolManager`.
- `AddPacket(Assembly)` and `AddPacket<TMarker>()` scan packet contracts, while `AddHandler<THandler>()` registers a single handler type.
- `AddHandlers(Assembly)` and `AddHandlers<TMarker>()` scan a whole assembly for packet controllers.
- `AddTcp<TProtocol>(...)` is the primary binding path for server startup, and `AddUdp<TProtocol>(...)` is available when the server needs UDP too.

## Core APIs

### `NetworkApplication`

`NetworkApplication` is the runnable entry point. It owns startup and shutdown order for:

- packet dispatcher activation
- protocol instantiation
- TCP/UDP listener management

### `INetworkApplicationBuilder`

The builder exposes fluent methods for common bootstrap concerns:

- `ConfigureLogging(...)`
- `ConfigureConnectionHub(...)`
- `ConfigureBufferPoolManager(...)`
- `Configure<TOptions>(...)`
- `AddPacket(...)`
- `AddHandlers(...)`
- `AddHandler(...)`
- `AddMetadataProvider(...)`
- `ConfigureDispatch(...)`
- `AddTcp<TProtocol>(...)`
- `AddUdp<TProtocol>(...)`

### Registration semantics

- `AddPacket<TMarker>()` scans the marker assembly for packet types.
- `AddHandlers<TMarker>()` scans the marker assembly for packet controller types.
- `AddHandler<THandler>()` registers one handler type explicitly.
- If you use the marker-based overloads, make the marker live in the assembly you actually want scanned.

For a method-by-method breakdown, see the dedicated API page: [Network Application](../api/hosting/network-application.md).

## Minimal example

```csharp
using Microsoft.Extensions.Logging;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Logging;
using Nalix.Network.Connections;
using Nalix.Network.Hosting;
using Nalix.Network.Options;
using Nalix.Runtime.Dispatching;

ILogger logger = NLogix.Host.Instance;

var app = NetworkApplication.CreateBuilder()
    .ConfigureLogging(logger)
    .ConfigureConnectionHub(new ConnectionHub())
    .Configure<NetworkSocketOptions>(options =>
    {
        options.Port = 57206;
    })
    .AddPacket<Handshake>()
    .AddHandlers<SampleHandlers>()
    .AddTcp<SampleProtocol>()
    .Build();

await app.RunAsync(cancellationToken);

[PacketController("SampleHandlers")]
public sealed class SampleHandlers
{
    [PacketOpcode(0x1001)]
    public ValueTask<Control> Handle(PacketContext<Control> request)
        => ValueTask.FromResult(new Control { Type = ControlType.PONG });
}

public sealed class SampleProtocol : Protocol
{
    private readonly IPacketDispatch _dispatch;

    public SampleProtocol(IPacketDispatch dispatch) => _dispatch = dispatch;

    public override void ProcessMessage(object sender, IConnectEventArgs args)
        => _dispatch.HandlePacket(args.Lease, args.Connection);
}
```

Custom packet handlers fit the same hosting model through `PacketContext<TPacket>` and the generic dispatch pipeline, so the same bootstrap works for built-in and custom packet types.

## Related packages

- [Nalix.Network](./nalix-network.md): Transport and listeners.
- [Nalix.Runtime](./nalix-runtime.md): Dispatcher and middleware.
- [Nalix.Common](./nalix-common.md): Shared primitives and attributes.

## Suggested reading

1. [Network Application API](../api/hosting/network-application.md)
2. [Packet Dispatch](../api/runtime/routing/packet-dispatch.md)
3. [Nalix.Network](./nalix-network.md)
