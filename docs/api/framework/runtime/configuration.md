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

### Configuration Attributes

Use these attributes to control how your options are serialized and documented in the `.ini` file:

- **`[IniComment("...")]`**: Adds a human-readable comment above a section or key.
    - *On Class*: Adds a comment at the top of the section.
    - *On Property*: Adds a comment above the specific configuration key.
    - *Note*: Use `\n` for multi-line comments.
- **`[ConfiguredIgnore]`**: Excludes a property from the configuration system. Use this for runtime-only state, internal caches, or calculated properties that shouldn't be persisted to disk.

**Example:**
```csharp
[IniComment("Server security settings")]
public sealed class SecurityOptions : ConfigurationLoader
{
    [IniComment("Max login attempts before IP ban")]
    public int MaxAttempts { get; set; } = 5;

    [ConfiguredIgnore("Resolved at runtime")]
    public string InternalToken { get; set; }
}
```

### Supported Binding Types

The configuration binder is optimized for speed and supports the following types directly:

| Category | Supported Types |
| :--- | :--- |
| **Primitives** | `bool`, `char`, `byte`, `sbyte`, `string` |
| **Numerics** | `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `float`, `double`, `decimal` |
| **Specialized** | `Guid`, `TimeSpan`, `DateTime`, `Enum` |

> [!WARNING]
> **Arrays and Collections (e.g., `int[]`, `List<T>`) are NOT supported.**
> To configure collections, use a comma-separated `string` and parse it manually in your component's constructor or a helper method.

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
