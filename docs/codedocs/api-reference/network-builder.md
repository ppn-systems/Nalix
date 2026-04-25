---
title: "Network Builder"
description: "Reference for INetworkApplicationBuilder, NetworkApplicationBuilder, Bootstrap, and hosting options."
---

`INetworkApplicationBuilder` and `NetworkApplicationBuilder` define the public bootstrap surface for server applications. `Bootstrap` and `HostingOptions` provide the hosting-wide defaults that support that builder.

## Import Paths

```csharp
using Nalix.Network.Hosting;
using Nalix.Network.Hosting.Options;
```

## Source

- [INetworkApplicationBuilder.cs](/workspace/home/nalix/src/Nalix.Network.Hosting/INetworkApplicationBuilder.cs)
- [NetworkApplicationBuilder.cs](/workspace/home/nalix/src/Nalix.Network.Hosting/NetworkApplicationBuilder.cs)
- [Bootstrap.cs](/workspace/home/nalix/src/Nalix.Network.Hosting/Bootstrap.cs)
- [Options/HostingOptions.cs](/workspace/home/nalix/src/Nalix.Network.Hosting/Options/HostingOptions.cs)

## Builder Surface

```csharp
public interface INetworkApplicationBuilder
{
    NetworkApplication Build();
    INetworkApplicationBuilder Configure<TOptions>(Action<TOptions> configure) where TOptions : ConfigurationLoader, new();
    INetworkApplicationBuilder ConfigureLogging(ILogger logger);
    INetworkApplicationBuilder ConfigureConnectionHub(IConnectionHub connectionHub);
    INetworkApplicationBuilder ConfigureBufferPoolManager(BufferPoolManager manager);
    INetworkApplicationBuilder ConfigureCertificate(string certificatePath);
    INetworkApplicationBuilder ConfigurePacketRegistry(IPacketRegistry packetRegistry);
    INetworkApplicationBuilder AddPacket(Assembly assembly, bool requirePacketAttribute = false);
    INetworkApplicationBuilder AddPacket(string assemblyPath, bool requirePacketAttribute = false);
    INetworkApplicationBuilder AddPacket<TMarker>(bool requirePacketAttribute = false);
    INetworkApplicationBuilder AddPacketNamespace(string packetNamespace, bool recursive = true);
    INetworkApplicationBuilder AddPacketNamespace(string assemblyPath, string packetNamespace, bool recursive = true);
    INetworkApplicationBuilder AddHandlers(Assembly assembly);
    INetworkApplicationBuilder AddHandlers<TMarker>();
    INetworkApplicationBuilder AddHandler<THandler>() where THandler : class;
    INetworkApplicationBuilder AddHandler<THandler>(Func<THandler> factory) where THandler : class;
    INetworkApplicationBuilder AddMetadataProvider<TProvider>() where TProvider : class, IPacketMetadataProvider;
    INetworkApplicationBuilder AddMetadataProvider<TProvider>(Func<TProvider> factory) where TProvider : class, IPacketMetadataProvider;
    INetworkApplicationBuilder ConfigureDispatch(Action<PacketDispatchOptions<IPacket>> configure);
    INetworkApplicationBuilder AddTcp<TProtocol>() where TProtocol : class, IProtocol;
    INetworkApplicationBuilder AddTcp<TProtocol>(Func<IPacketDispatch, TProtocol> factory) where TProtocol : class, IProtocol;
    INetworkApplicationBuilder AddTcp<TProtocol>(ushort port) where TProtocol : class, IProtocol;
    INetworkApplicationBuilder AddTcp<TProtocol>(ushort port, Func<IPacketDispatch, TProtocol> factory) where TProtocol : class, IProtocol;
    INetworkApplicationBuilder AddUdp<TProtocol>() where TProtocol : class, IProtocol;
    INetworkApplicationBuilder AddUdp<TProtocol>(Func<IConnection, EndPoint, ReadOnlySpan<byte>, bool> authen) where TProtocol : class, IProtocol;
    INetworkApplicationBuilder AddUdp<TProtocol>(Func<IPacketDispatch, TProtocol> factory) where TProtocol : class, IProtocol;
    INetworkApplicationBuilder AddUdp<TProtocol>(Func<IPacketDispatch, TProtocol> factory, Func<IConnection, EndPoint, ReadOnlySpan<byte>, bool> authen) where TProtocol : class, IProtocol;
    INetworkApplicationBuilder AddUdp<TProtocol>(ushort port) where TProtocol : class, IProtocol;
    INetworkApplicationBuilder AddUdp<TProtocol>(ushort port, Func<IConnection, EndPoint, ReadOnlySpan<byte>, bool> authen) where TProtocol : class, IProtocol;
    INetworkApplicationBuilder AddUdp<TProtocol>(ushort port, Func<IPacketDispatch, TProtocol> factory) where TProtocol : class, IProtocol;
    INetworkApplicationBuilder AddUdp<TProtocol>(ushort port, Func<IPacketDispatch, TProtocol> factory, Func<IConnection, EndPoint, ReadOnlySpan<byte>, bool> authen) where TProtocol : class, IProtocol;
}
```

## Key Methods

### `Configure<TOptions>`

```csharp
INetworkApplicationBuilder Configure<TOptions>(Action<TOptions> configure)
    where TOptions : ConfigurationLoader, new();
```

Mutates a typed configuration object loaded through `ConfigurationManager.Instance.Get<TOptions>()`, then invokes `Validate()` when that method exists.

### `AddPacket...`

These overloads control packet discovery:

| Method | Use when |
|---|---|
| `AddPacket<TMarker>()` | You want to scan the assembly that contains a known marker type. |
| `AddPacket(Assembly)` | You already have the assembly instance. |
| `AddPacket(string assemblyPath)` | Contracts are loaded from a `.dll` path. |
| `AddPacketNamespace(...)` | You want namespace-based discovery instead of assembly-wide scanning. |

### `AddHandler...`

Use `AddHandler<THandler>()` for a single controller or `AddHandlers<TMarker>()` to scan a whole assembly for `[PacketController]` classes.

### `ConfigureDispatch`

```csharp
INetworkApplicationBuilder ConfigureDispatch(Action<PacketDispatchOptions<IPacket>> configure);
```

Gives you the same `PacketDispatchOptions<IPacket>` surface used by direct runtime construction. This is where loop count, middleware, logging, and error handling are normally configured.

### `AddTcp...` and `AddUdp...`

These methods bind one or more protocol types to hosted listeners. UDP overloads optionally accept a datagram authentication predicate.

## Example

```csharp
using NetworkApplication app = NetworkApplication.CreateBuilder()
    .ConfigureLogging(logger)
    .Configure<NetworkSocketOptions>(options => options.Port = 57206)
    .Configure<DispatchOptions>(options => options.MaxPerConnectionQueue = 4096)
    .AddPacket<Handshake>()
    .AddHandlers<MyHandlers>()
    .ConfigureDispatch(options =>
    {
        options.WithDispatchLoopCount(8)
               .WithLogging(logger);
    })
    .AddTcp<MyProtocol>()
    .Build();
```

## `Bootstrap`

```csharp
public static partial class Bootstrap
{
    public static void Initialize();
}
```

`Bootstrap.Initialize()` switches the server configuration file to `server.ini`, enables packet pooling, materializes common option types, applies `HostingOptions`, flushes configuration, and prints the startup banner when appropriate.

### Example

```csharp
Nalix.Network.Hosting.Bootstrap.Initialize();
```

You rarely need to call this manually because the hosting assembly uses a module initializer to invoke it automatically.

## `HostingOptions`

```csharp
public sealed class HostingOptions : ConfigurationLoader
{
    public bool DisableConsoleClear { get; set; } = false;
    public bool DisableStartupBanner { get; set; } = false;
    public int MinWorkerThreads { get; set; } = 0;
    public int MinCompletionPortThreads { get; set; } = 0;
    public bool EnableGlobalExceptionHandling { get; set; } = true;
    public bool EnableHighPrecisionTimer { get; set; } = true;
    public static void Validate();
}
```

| Property | Type | Default | Description |
|---|---|---|---|
| `DisableConsoleClear` | `bool` | `false` | Leaves the existing console content intact at startup. |
| `DisableStartupBanner` | `bool` | `false` | Suppresses the banner and startup diagnostics. |
| `MinWorkerThreads` | `int` | `0` | Optional minimum worker thread floor for the ThreadPool. |
| `MinCompletionPortThreads` | `int` | `0` | Optional minimum IOCP thread floor. |
| `EnableGlobalExceptionHandling` | `bool` | `true` | Registers global exception handlers in bootstrap. |
| `EnableHighPrecisionTimer` | `bool` | `true` | Calls `timeBeginPeriod(1)` on Windows for timer precision. |

## Related Types

- [NetworkApplication](/docs/api-reference/network-application)
- [Dispatch Runtime](/docs/api-reference/dispatch-runtime)
