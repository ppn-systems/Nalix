# Logging

`Nalix.Logging` provides the built-in logger implementation used across the Nalix stack.

## Source mapping

- `src/Nalix.Logging/NLogix.cs`
- `src/Nalix.Logging/NLogix.Host.cs`
- `src/Nalix.Logging/Extensions/NLogixFx.cs`
- `src/Nalix.Logging/Extensions/NLogixFx.Internal.cs`
- `src/Nalix.Logging/Extensions/NLogixFx.Level.cs`
- `src/Nalix.Logging/Options/NLogixOptions.cs`
- `src/Nalix.Logging/Options/FileLogOptions.cs`
- `src/Nalix.Logging/Options/ConsoleLogOptions.cs`
- `src/Nalix.Logging/NLogixDistributor.cs`

## Main types

- `NLogix`
- `NLogix.Host`
- `NLogixOptions`
- `NLogixFx`
- `NLogixDistributor`
- `INLogixTarget`

## What it does

- implements `ILogger`
- supports multiple targets
- allows programmatic configuration
- works well as the shared logger registered through `InstanceManager`

## Basic usage

```csharp
using Nalix.Logging;

NLogix logger = NLogix.Host.Instance;

logger.Info("server-started");
logger.Warn("slow-handler");
logger.Error("dispatch-failed");
```

## Custom setup

```csharp
using Microsoft.Extensions.Logging;
using Nalix.Logging;
using Nalix.Logging.Sinks;

NLogix logger = new(cfg =>
{
    cfg.SetMinimumLevel(LogLevel.Debug)
       .RegisterTarget(new BatchConsoleLogTarget())
       .RegisterTarget(new BatchFileLogTarget());
});
```

## Typical integration

```csharp
using Microsoft.Extensions.Logging;
using Nalix.Framework.Injection;
using Nalix.Logging;

InstanceManager.Instance.Register<ILogger>(NLogix.Host.Instance);
```

This is the usual pattern for server startup so listeners, dispatch, and framework services use the same logger instance.

## NLogixDistributor

`NLogixDistributor` is the internal fan-out component that forwards published entries to registered targets.

## Source mapping

- `src/Nalix.Logging/NLogixDistributor.cs`

It is responsible for:

- registering `INLogixTarget` implementations
- publishing each entry to every registered target
- coordinating target lifetime and disposal

`NLogix` owns one distributor instance and uses it as the publish path after level filtering.

## Notes

- keep one shared logger for the process when possible
- prefer registering targets during startup, not mid-flight
- `NLogix` applies both console and file targets by default when you construct it without a custom configuration delegate
- `NLogix.Host.Instance` currently boots a shared logger with a console target and `Debug` minimum level

## Related APIs

- [Diagnostics Contracts](../common/diagnostics-contracts.md)
- [Configuration and DI](../framework/runtime/configuration.md)
- [Logging Extensions](./extensions.md)
- [Logging Targets](./targets.md)
- [Nalix.Logging](../../packages/nalix-logging.md)
