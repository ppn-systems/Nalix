# Configuration

This page documents the configuration runtime in `Nalix.Framework`, centered on:

- `ConfigurationManager`
- `ConfigurationLoader`

## Source mapping

- `src/Nalix.Framework/Configuration/ConfigurationManager.cs`
- `src/Nalix.Framework/Configuration/Binding/ConfigurationLoader.cs`
- `src/Nalix.Framework/Configuration/Binding/ConfigurationLoader.Metadata.cs`
- `src/Nalix.Framework/Configuration/Binding/ConfigurationLoader.SectionName.cs`
- `src/Nalix.Framework/Configuration/Binding/ConfigurationLoader.ValueParser.cs`
- `src/Nalix.Framework/Configuration/Internal/IniConfig.cs`

## ConfigurationManager

`ConfigurationManager` is the singleton entry point for loading and reloading typed options from an INI file.

### What it does

- resolves and validates the active config file path
- lazily initializes typed option containers via `Get<TClass>()`
- caches initialized option instances
- reloads existing containers when file changes are detected
- debounces file watcher events before reload
- flushes pending writes through `Flush()`

### Key members

- `Get<TClass>()`
- `Get<TClass>(string configFilePath, bool autoReload = true)`
- `SetConfigFilePath(string newConfigFilePath, bool autoReload = true)`
- `ReloadAll()`
- `Flush()`
- `IsLoaded<TClass>()`
- `Remove<TClass>()`
- `ClearAll()`
- `ConfigFilePath`
- `ConfigFileExists`
- `LastReloadTime`

### Basic usage

```csharp
NetworkSocketOptions socket = ConfigurationManager.Instance.Get<NetworkSocketOptions>();
socket.Validate();

TransportOptions transport = ConfigurationManager.Instance.Get<TransportOptions>();
transport.Validate();
```

### Runtime notes (source-verified)

- `SetConfigFilePath(...)` and `ReloadAll()` are `void` APIs and throw on failure/timeout.
- path changes are restricted to the allowed configuration directory.
- reload/path-change operations are serialized by a gate (`SemaphoreSlim`) and protected by read/write locks.
- watcher reload is debounce-based (300 ms) to absorb bursty file-system events.

!!! important "Changes are not automatically persisted to disk"
    Call `ConfigurationManager.Instance.Flush()` when you need to commit in-memory changes to the physical `.ini` file.

## ConfigurationLoader

Typed option classes should inherit from `ConfigurationLoader`.

```csharp
public sealed class MyServerOptions : ConfigurationLoader
{
    public string Name { get; set; } = "sample";
    public int Port { get; set; } = 57206;
}
```

### Section naming

Section names are derived from type names with suffix trimming (`Config`, `Option`, `Options`, `Setting`, `Settings`, `Configuration`, `Configurations`), then capitalized.

Example:

- `ConnectionHubOptions` -> `[ConnectionHub]`

### Attributes

- `[IniComment("...")]` adds section/key comments in generated INI.
- `[ConfiguredIgnore("...")]` excludes a property from binding.

```csharp
[IniComment("Server security settings")]
public sealed class SecurityOptions : ConfigurationLoader
{
    [IniComment("Max login attempts before IP ban")]
    public int MaxAttempts { get; set; } = 5;

    [ConfiguredIgnore("Resolved at runtime")]
    public string InternalToken { get; set; } = string.Empty;
}
```

### Supported binding types

| Category | Supported types |
| :--- | :--- |
| Primitives | `bool`, `char`, `byte`, `sbyte`, `string` |
| Numerics | `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `float`, `double`, `decimal` |
| Specialized | `DateTime`, `Guid`, `TimeSpan`, `Enum` |

!!! warning
    Arrays and collections (for example `int[]`, `List<T>`) are not directly supported by the binder.

## Common operations

```csharp
ConfigurationManager.Instance.SetConfigFilePath(
    @"E:\config\staging.ini",
    autoReload: true);

ConfigurationManager.Instance.ReloadAll();
ConfigurationManager.Instance.Flush();
```

## Related APIs

- [Instance Manager (DI)](./instance-manager.md)
- [Task Manager](./task-manager.md)
- [SingletonBase](./singleton-base.md)
- [Directories](../environment/directories.md)
- [Network Options](../../network/options/options.md)
