---
title: "Dispatch Runtime"
description: "Reference for PacketDispatchChannel, PacketDispatchOptions, PacketContext, metadata providers, packet sender, and dispatch options."
---

These types make up the server-side execution runtime that turns inbound packets into controller method calls.

## Import Paths

```csharp
using Nalix.Runtime.Dispatching;
using Nalix.Network.Routing;
using Nalix.Runtime.Options;
```

## Source

- [Dispatching/IPacketDispatch.cs](/workspace/home/nalix/src/Nalix.Runtime/Dispatching/IPacketDispatch.cs)
- [Dispatching/PacketDispatchChannel.cs](/workspace/home/nalix/src/Nalix.Runtime/Dispatching/PacketDispatchChannel.cs)
- [Dispatching/PacketContext.cs](/workspace/home/nalix/src/Nalix.Runtime/Dispatching/PacketContext.cs)
- [Dispatching/PacketSender.cs](/workspace/home/nalix/src/Nalix.Runtime/Dispatching/PacketSender.cs)
- [Dispatching/PacketMetadataBuilder.cs](/workspace/home/nalix/src/Nalix.Runtime/Dispatching/PacketMetadataBuilder.cs)
- [Dispatching/PacketMetadataProviders.cs](/workspace/home/nalix/src/Nalix.Runtime/Dispatching/PacketMetadataProviders.cs)
- [Dispatching/Options/PacketDispatchOptions.PublicMethods.cs](/workspace/home/nalix/src/Nalix.Runtime/Dispatching/Options/PacketDispatchOptions.PublicMethods.cs)
- [Options/DispatchOptions.cs](/workspace/home/nalix/src/Nalix.Runtime/Options/DispatchOptions.cs)

## `IPacketDispatch`

```csharp
public interface IPacketDispatch : IActivatable, IReportable
{
    void HandlePacket(IBufferLease packet, IConnection connection);
}
```

This is the minimal contract that protocols depend on.

## `PacketDispatchChannel`

```csharp
public sealed class PacketDispatchChannel
    : PacketDispatcherBase<IPacket>, IPacketDispatch, IDisposable, IActivatable
{
    public PacketDispatchChannel(Action<PacketDispatchOptions<IPacket>> options);
    public void Activate(CancellationToken cancellationToken = default);
    public void Deactivate(CancellationToken cancellationToken = default);
    public void HandlePacket(IBufferLease packet, IConnection connection);
    public string GenerateReport();
    public IDictionary<string, object> GetReportData();
    public void Dispose();
}
```

### Constructor

| Parameter | Type | Default | Description |
|---|---|---|---|
| `options` | `Action<PacketDispatchOptions<IPacket>>` | — | Configures handlers, middleware, logging, and loop behavior. |

### Example

```csharp
PacketDispatchChannel dispatch = new(options =>
{
    options.WithLogging(logger)
           .WithHandler(() => new MyHandlers());
});
```

## `PacketDispatchOptions<TPacket>`

```csharp
public sealed partial class PacketDispatchOptions<TPacket>
    where TPacket : IPacket
{
    public PacketDispatchOptions();
    public ILogger? Logging { get; }
    public int? DispatchLoopCount { get; }
    public int MaxDrainPerWakeMultiplier { get; set; } = 8;
    public int MinDrainPerWake { get; set; } = 64;
    public int MaxDrainPerWake { get; set; } = 2048;
    public int MinDispatchLoops { get; set; } = 1;
    public int MaxDispatchLoops { get; set; } = 64;
    public int MaxInternalQueueSize { get; set; } = 100_000;
    public PacketDispatchOptions<TPacket> WithLogging(ILogger logger);
    public PacketDispatchOptions<TPacket> WithErrorHandling(Action<Exception, ushort> errorHandler);
    public PacketDispatchOptions<TPacket> WithMiddleware(IPacketMiddleware<TPacket> middleware);
    public PacketDispatchOptions<TPacket> WithDispatchLoopCount(int? loopCount);
    public PacketDispatchOptions<TPacket> WithErrorHandlingMiddleware(bool continueOnError, Action<Exception, Type>? errorHandler = null);
    public PacketDispatchOptions<TPacket> WithHandler<TController>() where TController : class, new();
    public PacketDispatchOptions<TPacket> WithHandler<TController>(TController instance) where TController : class;
    public PacketDispatchOptions<TPacket> WithHandler<TController>(Func<TController> factory) where TController : class;
}
```

### Key Methods

| Method | Description |
|---|---|
| `WithLogging` | Attaches a logger used by the dispatcher. |
| `WithErrorHandling` | Sets a custom error callback for handler failures. |
| `WithMiddleware` | Adds a packet middleware component. |
| `WithDispatchLoopCount` | Overrides the worker loop count. |
| `WithErrorHandlingMiddleware` | Controls middleware failure behavior. |
| `WithHandler` | Scans a controller for `[PacketOpcode]` methods and registers them. |

### Example

```csharp
options.WithDispatchLoopCount(8)
       .WithErrorHandling((ex, opcode) => logger.LogError(ex, "opcode={Opcode}", opcode))
       .WithHandler(() => new MyHandlers());
```

## `PacketContext<TPacket>`

```csharp
public sealed class PacketContext<TPacket> : IPacketContext<TPacket>, IPoolable, IDisposable
    where TPacket : IPacket
{
    public bool IsReliable { get; }
    public bool SkipOutbound { get; internal set; }
    public TPacket Packet { get; }
    public IConnection Connection { get; }
    public PacketMetadata Attributes { get; }
    public IPacketSender Sender { get; }
    public CancellationToken CancellationToken { get; }
    public void ResetForPool();
    public void Return();
    public void Dispose();
}
```

`PacketContext<TPacket>` is the handler input when you use `IPacketContext<TPacket>` parameters in controller methods.

## `PacketSender`

```csharp
public sealed class PacketSender : IPacketSender, IPoolable
{
    public void ResetForPool();
    public void Initialize<TPacket>(IPacketContext<TPacket> context) where TPacket : IPacket;
    public ValueTask SendAsync(IPacket packet, CancellationToken ct = default);
    public ValueTask SendAsync(IPacket packet, bool forceEncrypt, CancellationToken ct = default);
}
```

It serializes the outbound packet, applies compression and encryption based on `PacketMetadata`, then sends it over the transport selected by the handler metadata.

## `PacketMetadataBuilder` and `PacketMetadataProviders`

```csharp
public sealed class PacketMetadataBuilder
{
    public PacketOpcodeAttribute? Opcode { get; set; }
    public PacketTimeoutAttribute? Timeout { get; set; }
    public PacketPermissionAttribute? Permission { get; set; }
    public PacketEncryptionAttribute? Encryption { get; set; }
    public PacketRateLimitAttribute? RateLimit { get; set; }
    public PacketConcurrencyLimitAttribute? ConcurrencyLimit { get; set; }
    public PacketTransportAttribute? Transport { get; set; }
    public void Add(Attribute attribute);
    public TAttribute? Get<TAttribute>() where TAttribute : Attribute;
    public PacketMetadata Build();
}

public static class PacketMetadataProviders
{
    public static void Register(IPacketMetadataProvider provider);
}
```

Use a metadata provider when handler registration needs to derive metadata from custom attributes.

## `DispatchOptions`

```csharp
public sealed class DispatchOptions : ConfigurationLoader
{
    public int MaxPerConnectionQueue { get; set; } = 4096;
    public DropPolicy DropPolicy { get; set; } = DropPolicy.DropNewest;
    public TimeSpan BlockTimeout { get; set; } = TimeSpan.FromMilliseconds(1000);
    public string PriorityWeights { get; set; } = "1,2,4,8,16";
    public int BucketCountMultiplier { get; set; } = 64;
    public int MinBucketCount { get; set; } = 256;
    public int MaxBucketCount { get; set; } = 16384;
    public void Validate();
}
```

| Property | Type | Default | Description |
|---|---|---|---|
| `MaxPerConnectionQueue` | `int` | `4096` | Per-connection queue bound; `0` disables the bound. |
| `DropPolicy` | `DropPolicy` | `DropNewest` | Overflow behavior. |
| `BlockTimeout` | `TimeSpan` | `00:00:01` | Timeout when blocking on enqueue. |
| `PriorityWeights` | `string` | `"1,2,4,8,16"` | Weighted round-robin priorities. |
| `BucketCountMultiplier` | `int` | `64` | Internal bucket sizing factor. |
| `MinBucketCount` | `int` | `256` | Lower bucket bound. |
| `MaxBucketCount` | `int` | `16384` | Upper bucket bound. |

## Related Types

- [Protocol and Network](/docs/api-reference/protocol-and-network)
- [Packet Framework](/docs/api-reference/packet-framework)
