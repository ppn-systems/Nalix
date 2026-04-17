# Network Application

`Nalix.Network.Hosting` provides a Microsoft-style builder and host for Nalix servers. It simplifies the setup of protocols, listeners, dispatchers, and dependency injection into a single fluent flow.

## Why use the Hosting Builder?

While you can instantiate listeners and protocols manually, the `NetworkApplicationBuilder` is **highly recommended** for production applications.

- **Unified Lifecycle**: Ensures that the memory pool, handler registry, dispatcher, and listeners are activated and deactivated in the correct order.
- **Automatic Service Injection**: Automatically registers critical shared services (Logger, ConnectionHub, BufferPool) into the `InstanceManager`.
- **Handler Discovery**: Scans assemblies for `[PacketController]` classes and performs high-performance **Handler Compilation** (Expression Trees) to eliminate reflection overhead.
- **Coexistence**: Easily manages multiple listeners (e.g., TCP and UDP) within the same application process.

The public API surface revolves around two main types:

- `NetworkApplication` is the runnable host.
- `INetworkApplicationBuilder` is the fluent configuration contract.

## Source mapping

- `src/Nalix.Network.Hosting/NetworkApplication.cs`
- `src/Nalix.Network.Hosting/INetworkApplicationBuilder.cs`
- `src/Nalix.Network.Hosting/NetworkApplicationBuilder.cs`

## Startup Flow

```mermaid
graph LR
    subgraph Configuration ["Phase 1: Configuration"]
        Create["CreateBuilder()"] --> Config["Configure Loggers, Options, Hubs"]
        Config --> Discover["AddPacket() & AddHandlers()"]
        Discover --> Bind["AddTcp() / AddUdp()"]
    end

    subgraph Composition ["Phase 2: Composition"]
        Bind --> Build["Build()"]
        Build --> Prep["Initialize Context"]
    end

    subgraph Activation ["Phase 3: Activation"]
        Prep --> Run["ActivateAsync()"]
        Run --> Infra["Infrastructure"]
        Infra --> Registry["Build Registry"]
        Registry --> Dispatch["Initalize Dispatch"]
        Dispatch --> Listeners["Start Listeners"]
    end
```

## Public members at a glance

| Type | Public members |
|---|---|
| `NetworkApplication` | `CreateBuilder()`, `ActivateAsync(...)`, `DeactivateAsync(...)`, `RunAsync(...)`, `Dispose()` |
| `INetworkApplicationBuilder` | `ConfigureLogging(...)`, `ConfigureConnectionHub(...)`, `ConfigureBufferPoolManager(...)`, `Configure<TOptions>(...)`, `AddPacket(...)`, `AddHandlers(...)`, `AddHandler(...)`, `AddMetadataProvider(...)`, `ConfigureDispatch(...)`, `AddTcp(...)`, `AddUdp(...)`, `Build()` |

## `NetworkApplication`

`NetworkApplication` manages the lifecycle of the server runtime. It handles the activation and deactivation of the packet dispatcher, protocols, and listeners in the correct order.
The hosted pipeline remains generic-friendly, so the same builder flow works for built-in packets and custom packet types.

### Lifecycle methods

- `ActivateAsync(...)`: Starts the application. It initializes the packet registry, activates the dispatcher, and starts all registered TCP and UDP listeners.
!!! note
    Middleware in context is registered globally but executed in the sharded dispatch loop. Ensure your custom middleware is thread-safe or uses localized state.
- `RunAsync(...)`: Activates the application and waits indefinitely until cancellation is requested, then deactivates it.
- `DeactivateAsync(...)`: Gracefully stops all listeners and disposes of protocols and the dispatcher.
- `Dispose()`: Synchronous cleanup that calls `DeactivateAsync`.

## `INetworkApplicationBuilder`

The builder uses a fluent API to configure the host before it is built.

### Logging and Options

- `ConfigureLogging(ILogger)`: Registers the logger into the `InstanceManager`.
- `ConfigureConnectionHub(IConnectionHub)`: Registers the shared connection hub into the `InstanceManager`.
- `ConfigureBufferPoolManager(BufferPoolManager)`: Explicitly registers a custom buffer pool manager and binds `BufferLease.ByteArrayPool` to that manager for pooled receive/send paths.
- `Configure<TOptions>(Action<TOptions>)`: Configures a specific options type. This is applied during the activation phase.

!!! note
    If you do not configure a connection hub or buffer pool manager, the builder can create default instances during build/activation. The built-in handler set is registered automatically before user-defined handler discovery runs.
    
    The builder also re-binds `BufferLease.ByteArrayPool` during startup, so the server receive path stays on pooled buffers even if `BufferLease` was touched before host wiring.

### Packet and Handler Discovery

- `AddPacket(assembly, requirePacketAttribute)`: Scans an assembly for packet types.
- `AddPacket<TMarker>(...)`: Marker-type shortcut for scanning packets.
- `AddHandlers(assembly)`: Scans an assembly for `[PacketController]` classes.
- `AddHandlers<TMarker>()`: Marker-type shortcut for scanning handlers.
- `AddHandler<THandler>()`: Manually registers a handler type.
- `AddHandler<THandler>(Func<THandler> factory)`: Registers a handler type with a custom factory.

### Metadata and Dispatch

- `AddMetadataProvider<TProvider>()`: Registers a packet metadata provider.
- `AddMetadataProvider<TProvider>(Func<TProvider> factory)`: Registers a metadata provider with a custom factory.
- `ConfigureDispatch(Action<PacketDispatchOptions<IPacket>>)`: Configures the `PacketDispatchChannel` options, including middleware and custom logic for built-in and custom packet pipelines.

### Server Bindings

- `AddTcp<TProtocol>()`: Registers a TCP server for the specified protocol.
- `AddTcp<TProtocol>(Func<IPacketDispatch, TProtocol> factory)`: Registers a TCP server with a custom protocol factory.
- `AddUdp<TProtocol>()`: Registers a UDP server for the specified protocol.
- `AddUdp<TProtocol>(Func<IConnection, EndPoint, ReadOnlySpan<byte>, bool> authen)`: Registers a UDP server with a custom authentication predicate.
- `AddUdp<TProtocol>(Func<IPacketDispatch, TProtocol> factory)`: Registers a UDP server with a custom protocol factory.
- `AddUdp<TProtocol>(Func<IPacketDispatch, TProtocol> factory, Func<IConnection, EndPoint, ReadOnlySpan<byte>, bool> authen)`: Registers a UDP server with both a custom factory and an authentication predicate.

## Basic usage

```csharp
var app = NetworkApplication.CreateBuilder()
    .ConfigureLogging(logger)
    .ConfigureConnectionHub(new ConnectionHub())
    .ConfigureBufferPoolManager(new BufferPoolManager())
    .Configure<NetworkSocketOptions>(options =>
    {
        options.Port = 57206;
    })
    .AddPacket<Handshake>()
    .AddHandlers<SampleHandlers>()
    .AddTcp<SampleProtocol>()
    .Build();

await app.RunAsync(cancellationToken);
```

## Zero-allocation receive checklist

To keep message reading allocation-free on the server hot path:

- register a `BufferPoolManager` with `ConfigureBufferPoolManager(...)` (or let the builder create one)
- keep protocol `ProcessMessage(...)` lease-based (`args.Lease`) and forward directly to dispatch
- avoid copying raw payload into new `byte[]` in middleware/protocol unless required by business logic

## Related APIs

- [Nalix.Network.Hosting package overview](../../packages/nalix-network-hosting.md)
- [Protocol](../network/protocol.md)
- [TCP Listener](../network/tcp-listener.md)
- [UDP Listener](../network/udp-listener.md)
- [Packet Dispatch](../runtime/routing/packet-dispatch.md)
- [Packet Registry](../framework/packets/packet-registry.md)
- [Configuration](../framework/runtime/configuration.md)
- [Instance Manager (DI)](../framework/runtime/instance-manager.md)
