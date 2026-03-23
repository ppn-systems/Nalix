# Configuration and DI

This page covers the two framework services most Nalix applications touch first:

- `ConfigurationManager`
- `InstanceManager`

## Source mapping

- `src/Nalix.Framework/Configuration/ConfigurationManager.cs`
- `src/Nalix.Framework/Configuration/Binding/ConfigurationLoader.cs`
- `src/Nalix.Framework/Configuration/Binding/ConfigurationLoader.Metadata.cs`
- `src/Nalix.Framework/Configuration/Binding/ConfigurationLoader.SectionName.cs`
- `src/Nalix.Framework/Injection/InstanceManager.cs`

## ConfigurationManager

`ConfigurationManager` loads typed option objects that derive from `ConfigurationLoader`.

It is responsible for:

- locating and watching the active INI file
- initializing option objects from sections
- caching loaded option instances
- reloading already-created option objects when the file changes

### Basic usage

```csharp
NetworkSocketOptions socket = ConfigurationManager.Instance.Get<NetworkSocketOptions>();
socket.Validate();

TransportOptions transport = ConfigurationManager.Instance.Get<TransportOptions>();
transport.Validate();
```

### Current runtime behavior

The current implementation in `src/Nalix.Framework` is:

- thread-safe
- watcher-based with debounce
- able to switch config file path through `SetConfigFilePath(...)`
- able to reload already-initialized containers through `ReloadAll()`

### ConfigurationLoader

Your typed options should inherit from `ConfigurationLoader`:

```csharp
public sealed class MyServerOptions : ConfigurationLoader
{
    public string Name { get; set; } = "sample";
    public int Port { get; set; } = 57206;
}
```

Section names are derived from the type name. A class such as `ConnectionHubOptions` maps to the `[ConnectionHubOptions]` section.

Use `IniCommentAttribute` for readable generated comments and `ConfiguredIgnoreAttribute` (or `[ConfiguredIgnore]`) for runtime-only properties.

### Common operations

```csharp
bool reloaded = ConfigurationManager.Instance.ReloadAll();

bool changed = ConfigurationManager.Instance.SetConfigFilePath(
    @"E:\config\staging.ini",
    autoReload: true);
```

## InstanceManager

`InstanceManager` is the shared service registry used throughout the Nalix stack.

Use it to:

- register a shared `ILogger`
- register an `IPacketRegistry`
- create or retrieve shared singleton-like services such as `TaskManager`

### Basic usage

```csharp
InstanceManager.Instance.Register<ILogger>(logger);
InstanceManager.Instance.Register<IPacketRegistry>(packetRegistry);

TaskManager taskManager = InstanceManager.Instance.GetOrCreateInstance<TaskManager>();
```

### What it actually does

The current runtime implementation provides:

- fast type-based caching
- optional interface registration when registering a concrete instance
- activator-based lazy creation
- disposable tracking for owned instances

## Typical startup pattern

```csharp
using Microsoft.Extensions.Logging;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.Injection;
using Nalix.Logging;

ILogger logger = NLogix.Host.Instance;
IPacketRegistry registry = BuildPacketRegistry();

InstanceManager.Instance.Register<ILogger>(logger);
InstanceManager.Instance.Register<IPacketRegistry>(registry);

NetworkSocketOptions socket = ConfigurationManager.Instance.Get<NetworkSocketOptions>();
socket.Validate();
```

## Related APIs

- [Task Manager](./task-manager.md)
- [Snowflake](./snowflake.md)
- [SingletonBase](./singleton-base.md)
- [Directories](../environment/directories.md)
- [Network Options](../../network/options/options.md)
- [Server Blueprint](../../../guides/server-blueprint.md)
