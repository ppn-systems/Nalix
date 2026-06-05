# Logging

`Nalix.Logging` provides the built-in logger implementation used across the Nalix stack.

## Source mapping

- `src/Nalix.Logging/NLogix.cs`
- `src/Nalix.Logging/NLogixBuilder.cs`
- `src/Nalix.Logging/Extensions/NLogixFx.cs`
- `src/Nalix.Logging/Extensions/NLogixFx.Internal.cs`
- `src/Nalix.Logging/Extensions/NLogixFx.Level.cs`
- `src/Nalix.Logging/Options/NLogixOptions.cs`
- `src/Nalix.Logging/Options/FileLogOptions.cs`
- `src/Nalix.Logging/Options/ConsoleLogOptions.cs`

## Main types

- `NLogix`
- `NLogixBuilder`
- `NLogixOptions`
- `NLogixFx`
- `INLogixTarget`
- `INLogixFormatter`
- `INLogixErrorHandler`
- `INLogixBuilder`

## What it does

- implements `ILogger`
- supports multiple targets
- allows programmatic configuration
- works well as the shared logger registered through `InstanceManager`

## Basic usage

```csharp
using Nalix.Logging.Extensions;

NLogix logger = NLogixFx.Logger;

logger.LogInformation("server-started");
logger.LogWarning("slow-handler");
logger.LogError("dispatch-failed");
```

## Custom setup

```csharp
using Microsoft.Extensions.Logging;
using Nalix.Logging;
using Nalix.Logging.Extensions;
using Nalix.Logging.Sinks;

NLogixFx.Configure(cfg =>
{
    cfg.SetMinimumLevel(LogLevel.Debug)
       .AddTarget(new BatchConsoleLogTarget())
       .AddTarget(new BatchFileLogTarget());
});

NLogix logger = NLogixFx.Logger;
```

## Typical integration

```csharp
using Microsoft.Extensions.Logging;
using Nalix.Framework.Injection;
using Nalix.Logging.Extensions;

InstanceManager.Instance.Register<ILogger>(NLogixFx.Logger);
```

This is the usual pattern for server startup so listeners, dispatch, and framework services use the same logger instance.

## NLogixBuilder

`NLogixBuilder` is the builder that accumulates configuration and produces an `NLogix` instance.

## Source mapping

- `src/Nalix.Logging/NLogixBuilder.cs`

It is responsible for:

- accumulating `INLogixTarget` registrations via `AddTarget(...)`
- applying options configurators
- building the final `NLogix` instance with `Build()`
- adding default console and file targets when no targets are explicitly registered

## Notes

- keep one shared logger for the process when possible
- prefer registering targets during startup, not mid-flight
- `NLogix` applies both console and file targets by default when you construct it via `NLogixBuilder` without explicit targets
- `NLogixFx.Logger` is the global shared logger, initialized with default targets during static construction

## Related APIs

- [Configuration](../environment/configuration.md)
- [Instance Manager (DI)](../framework/instance-manager.md)
- [Logging Extensions](./extensions.md)
- [Logging Targets](./targets.md)
- [Nalix.Logging](../../packages/nalix-logging.md)
