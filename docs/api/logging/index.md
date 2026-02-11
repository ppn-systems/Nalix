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
- `src/Nalix.Logging/Engine/NLogixDistributor.cs`

## Main types

- `NLogix`
- `NLogix.Host`
- `NLogixOptions`
- `NLogixFx`
- `INLogixTarget`

## What it does

- implements `ILogger`
- supports multiple targets
- allows programmatic configuration
- works well as the shared logger registered through `InstanceManager`

## Basic usage

```csharp
NLogix logger = NLogix.Host.Instance;

logger.Info("server-started");
logger.Warn("slow-handler");
logger.Error("dispatch-failed");
```

## Custom setup

```csharp
NLogix logger = new(cfg =>
{
    cfg.SetMinimumLevel(LogLevel.Debug)
       .RegisterTarget(new BatchConsoleLogTarget())
       .RegisterTarget(new BatchFileLogTarget());
});
```

## Typical integration

```csharp
InstanceManager.Instance.Register<ILogger>(NLogix.Host.Instance);
```

This is the usual pattern for server startup so listeners, dispatch, and framework services use the same logger instance.

## NLogixEngine

`NLogixEngine` is the abstract high-performance base used by the concrete logging runtime.

## Source mapping

- `src/Nalix.Logging/Engine/NLogixEngine.cs`

It is responsible for:

- caching and applying `NLogixOptions`
- checking minimum log level before formatting
- publishing `LogEntry` objects into `NLogixDistributor`
- owning common formatting helpers and disposal behavior

This is the type that turns a logging call into a published entry while keeping the hot path fast.

## NLogixConstants

`NLogixConstants` contains the small formatting constants shared by the logging implementation.

## Source mapping

- `src/Nalix.Logging/Engine/NLogixConstants.cs`

Current constants include:

- `LogBracketOpen`
- `LogBracketClose`
- `LogSpaceSeparator`
- `LogDashSeparator`
- `DefaultLogBufferSize`

## Notes

- keep one shared logger for the process when possible
- prefer registering targets during startup, not mid-flight
- use `NLogix.Host.Instance` unless you have a good reason to own a separate logger graph

## Related APIs

- [Diagnostics Contracts](../common/diagnostics-contracts.md)
- [Configuration and DI](../framework/runtime/configuration.md)
- [Logging Extensions](./extensions.md)
- [Logging Targets](./targets.md)
- [Nalix.Logging](../../packages/nalix-logging.md)
