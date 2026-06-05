# Nalix.Logging

`Nalix.Logging` provides the structured logging subsystem for Nalix applications. It implements `Microsoft.Extensions.Logging.ILogger` and adds asynchronous batching, pluggable sink targets, and configuration options optimized for high-throughput server workloads.

## Bootstrap

Register the logger once during startup and reuse it across the process via `InstanceManager`.

```csharp
using Microsoft.Extensions.Logging;
using Nalix.Framework.Injection;
using Nalix.Logging.Extensions;

ILogger logger = NLogixFx.Logger;
InstanceManager.Instance.Register<ILogger>(logger);
```

`NLogixFx.Logger` provides the global default logger initialized with console and file targets. For custom configuration, use the builder pattern:

```csharp
using Nalix.Logging.Extensions;
using Nalix.Logging.Sinks;

NLogixFx.Configure(cfg =>
{
    cfg.SetMinimumLevel(LogLevel.Debug)
       .ConfigureFileOptions(f =>
       {
           f.LogFileName = "server.log";
       })
       .AddTarget(new BatchConsoleLogTarget())
       .AddTarget(new BatchFileLogTarget());
});
```

## Core Components

| Component | Purpose |
| --- | --- |
| `NLogix` | Logger implementation with batched asynchronous output |
| `NLogixFx` | Static extensions providing the global `Logger` instance and `Configure` method |
| `NLogixOptions` | Configuration for minimum level, timestamps, and metadata |
| `BatchConsoleLogTarget` | High-throughput batched console sink |
| `BatchFileLogTarget` | Asynchronous file sink with configurable file name and rotation |
| `INLogixTarget` | Interface for implementing custom log sinks |

## Custom Log Targets

Implement `INLogixTarget` to create custom sinks for external systems (SIEM, databases, message queues):

```csharp
public sealed class MyCustomTarget : INLogixTarget
{
    public void Publish(
        DateTime timestampUtc,
        LogLevel logLevel,
        EventId eventId,
        string message,
        Exception? exception)
    {
        // Forward to external system
    }
}

// Register during logger configuration
NLogixFx.Configure(cfg =>
{
    cfg.AddTarget(new MyCustomTarget());
});
```

## Log Level Guidance

| Level | Use for |
| --- | --- |
| `Trace` | Internal framework diagnostics (packet registry binding, dispatch trace) |
| `Debug` | Application debugging (handler entry/exit, middleware decisions) |
| `Information` | Operational events (server started, connection accepted) |
| `Warning` | Recoverable issues (connection guard rejection, timeout) |
| `Error` | Failures requiring attention (dispatch error, handler exception) |
| `Critical` | Unrecoverable failures (listener crash, startup failure) |

## Usage with Nalix.Hosting

When using the hosting builder, register the logger via `ConfigureLogging`:

```csharp
var app = NetworkApplication.CreateBuilder()
    .ConfigureLogging(logger)
    .BindTcp<MyProtocol>().Bind()
    .Build();
```

!!! warning "Dispose with care"
    Dispose the logger only when your process is shutting down. `NLogixOptions` is owned by the logger runtime. Early disposal will stop all logging output.

## Key API Pages

- [Logging Overview](../api/logging/index.md)
- [Logging Options](../api/options/network/options.md)
- [Logging Extensions](../api/logging/extensions.md)
- [Logging Targets](../api/logging/targets.md)
