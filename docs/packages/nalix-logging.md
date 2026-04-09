# Nalix.Logging

Built-in structured logging for the Nalix stack, with shared `ILogger` integration and batched console/file targets.

### Logging bootstrap
Register the logger once and reuse it across the process.

**Key Components**
- `NLogix`
- `NLogix.Host`
- `ILogger`

### Quick example

```csharp
using Microsoft.Extensions.Logging;
using Nalix.Framework.Injection;
using Nalix.Logging;

InstanceManager.Instance.Register<ILogger>(NLogix.Host.Instance);
```

### Options and targets
Options describe log level, timestamps, metadata, and sink behavior.

**Key Components**
- `NLogixOptions`
- `FileLogOptions`
- `ConsoleLogOptions`
- `INLogixTarget`

### Quick example

```csharp
using Microsoft.Extensions.Logging;
using Nalix.Logging;
using Nalix.Logging.Options;
using Nalix.Logging.Sinks;

NLogix logger = new(cfg =>
{
    cfg.SetMinimumLevel(LogLevel.Debug)
       .ConfigureFileOptions(f => f.LogFileName = "server.log")
       .RegisterTarget(new BatchConsoleLogTarget())
       .RegisterTarget(new BatchFileLogTarget());
});
```

!!! warning "Dispose with care"
    Dispose the logger only when your process is shutting down. `NLogixOptions` is owned by the logger runtime.

## Key API pages

- [Logging](../api/logging/index.md)
- [Logging Options](../api/logging/options.md)
- [Logging Extensions](../api/logging/extensions.md)
- [Logging Targets](../api/logging/targets.md)
