# Nalix.Logging API Documentation

_A high-performance, extensible logging framework for .NET 10 (multi-OS) supporting structured logging, batch processing, and flexible output targets._

- **Supported .NET:** 10  
- **Supported OS:** Windows, Linux, MacOS  
- **NuGet:** [`Nalix.Logging`](https://www.nuget.org/packages/Nalix.Logging)  
- **Dependencies:** [`Nalix.Common`](https://www.nuget.org/packages/Nalix.Common), [`Nalix.Framework`](https://www.nuget.org/packages/Nalix.Framework)

---

## Table of Contents

- [Installation](#installation)
- [Quick Start](#quick-start)
- [Core Public API](#core-public-api)
  - [NLogix](#nlogix)
  - [NLogix.Host (Singleton Logger)](#nlogixhost-singleton-logger)
  - [Logging Targets](#logging-targets)
  - [NLogixOptions (Configuration)](#nlogixoptions-configuration)
  - [NLogixExtensions (Contextual Logging)](#nlogixextensions-contextual-logging)
  - [ConfigurationManager](#configurationmanager)
- [Configuration](#configuration)
- [Custom Logging Targets](#custom-logging-targets)
- [Troubleshooting](#troubleshooting)
- [License](#license)

---

## Installation

```shell
dotnet add package Nalix.Logging
```

Requires [.NET 10](https://dotnet.microsoft.com/).

---

## Quick Start

### 1. Create and Use the Default Logger

```csharp
using Nalix.Logging;

// Get the global singleton logger instance
NLogix logger = NLogix.Host.Instance;

// Log messages at different levels
logger.Info("Hello from Nalix!");
logger.Warn("Something may need your attention.");
logger.Error("An error occurred.");
logger.Fatal("Critical failure happened.");
```

### 2. Advanced: Custom Logger Configuration

```csharp
using Nalix.Logging;
using Nalix.Logging.Sinks;

// Create a logger with custom options
NLogix myLogger = new(cfg =>
{
    cfg.SetMinimumLevel(Nalix.Common.Diagnostics.Enums.LogLevel.Debug) // Set minimum log level
       .RegisterTarget(new BatchFileLogTarget())                      // Log to file (batched)
       .RegisterTarget(new BatchConsoleLogTarget());                  // Log to console (batched)
});
```

### 3. Contextual Logging with Extensions

You can include the class and member context automatically for easy tracing:

```csharp
using Nalix.Logging;
using Nalix.Logging;

public class MyService
{
    private readonly ILogger _logger = NLogix.Host.Instance;

    public void DoWork()
    {
        _logger.Info<MyService>("Start"); // Log: [MyService:DoWork] Start
        try
        {
            // ...
        }
        catch (Exception ex)
        {
            _logger.Error<MyService>(ex);
        }
    }
}
```

---

## Core Public API

### NLogix

Main logging engine implementing `ILogger`.

**How to use:**

- Direct construction (`new NLogix(...)`)
- Using the singleton via `NLogix.Host.Instance` (recommended in most apps)

**Main Methods:**

```csharp
void Meta(string message);
void Trace(string message);
void Debug(string message);
void Info(string message);
void Warn(string message);
void Error(string message, Exception? ex = null);
void Fatal(string message, Exception? ex = null);
```

- Overloads available for formatted messages and specifying event IDs.

**Example:**

```csharp
logger.Info("Process completed.");
logger.Debug("Debug info: {0}", value);
```

---

### NLogix.Host (Singleton Logger)

Static singleton provider for the global logger instance.

**Usage:**

```csharp
NLogix logger = NLogix.Host.Instance;
// Now use logger for all your application logging
```

**How it works:**  

- Lazily initialized, thread-safe, default targets: Batch file + Batch console.

---

### Logging Targets

#### BatchFileLogTarget

Batched, asynchronous file logging.

- **Typical usage:** Add as a logging target for NLogix via `RegisterTarget`.
- **Custom configuration:** Pass `FileLogOptions` or use an action to configure.

```csharp
// Use defaults:
cfg.RegisterTarget(new BatchFileLogTarget());

// Custom config:
cfg.RegisterTarget(new BatchFileLogTarget(opts => {
    opts.MaxFileSizeBytes = 5 * 1024 * 1024; // 5MB max per file
    opts.LogFileName = "myapp.log";
}));
```

#### BatchConsoleLogTarget

Batched, asynchronous console output.

```csharp
cfg.RegisterTarget(new BatchConsoleLogTarget());
```

**Note:** You can add multiple targets. Targets are thread-safe and high-performance.

---

### NLogixOptions (Configuration)

Controls main logging behavior. You can pass options when initializing `NLogix` directly, or set via `ConfigurationManager`.

**Notable options:**

| Property                                       | Description                                    |
|------------------------------------------------|------------------------------------------------|
| `MinLevel`                                     | Minimum log level to record (e.g. Debug/Info)  |
| `UseUtcTimestamp`                              | Use UTC time in logs.                          |
| `IncludeProcessId`                             | Log the process ID with entries.               |
| `IncludeTimestamp`                             | Add timestamp to log line.                     |
| `ConfigureFileOptions(Action<FileLogOptions>)` | Configure file logging behavior.               |
| `RegisterTarget(ILoggerTarget)`                | Add file/console/custom targets.               |

**Example:**  

```csharp
var log = new NLogix(cfg =>
{
    cfg.SetMinimumLevel(LogLevel.Debug);
    cfg.ConfigureFileOptions(opt => opt.LogFileName = "prod.log");
});
```

> All options can also be set programmatically or through `.ini` file using `ConfigurationManager`.

---

### NLogixExtensions (Contextual Logging)

Extension methods for logging with class/member context automatically tagged.

**Methods (generic):**

```csharp
logger.Info<T>("msg");    // [T:Member] msg
logger.Debug<T>("detail");
logger.Error<T>(Exception ex);
// ... etc for Trace/Meta/Warn/Fatal
```

Requires `using Nalix.Logging;`

---

### ConfigurationManager

`ConfigurationManager` lets you load and save configuration (including logger settings) from `.ini` files.

- **Purpose:** Centralized, fast configuration for libraries/apps.
- **Usage:** Normally advanced; see below for example.

**Example:**

```csharp
using Nalix.Framework.Configuration;

// Change config file (auto reload)
ConfigurationManager.Instance.SetConfigFilePath("myconfig.ini");

// Access config for a type you defined (inherits from ConfigurationLoader)
var myConfig = ConfigurationManager.Instance.Get<MyAppConfig>();

// Modify and flush
myConfig.SomeSetting = true;
ConfigurationManager.Instance.Flush();
```

**Tip:** Logger (NLogix) options can be controlled by reading custom `.ini` via this manager.

---

## Configuration

- Most settings (logger options, file/console details) can be set
  - in code (recommended) **OR**
  - via `.ini` file using `ConfigurationManager` (for enterprise/multi-deployment use).

**Logger config often looks like:**

```csharp
var logger = new NLogix(cfg =>
{
    cfg.SetMinimumLevel(LogLevel.Information);
    cfg.RegisterTarget(new BatchFileLogTarget());
});
```

Or, to let config auto-load from file:

```csharp
var opts = ConfigurationManager.Instance.Get<NLogixOptions>("nlogix.ini");
var logger = new NLogix(cfg => { /* apply settings from opts */ });
```

---

## Custom Logging Targets

Want to add your own logging output?  
Just implement `ILoggerTarget`, and register via `RegisterTarget` in options.

```csharp
public class MyCustomTarget : ILoggerTarget { ... }

// During config:
cfg.RegisterTarget(new MyCustomTarget());
```

---

## Troubleshooting

- **Missing log entries or file not created:**  
  Check file path and permissions, and ensure you have registered at least one target.
- **Performance:**  
  All built-in targets are batched. If you're seeing slow log writing, check your batch size settings.

---

## License

Licensed under the Apache License, Version 2.0.  
See [LICENSE](./LICENSE) for details.

---

## Feedback & Contributions

See CONTRIBUTING.md in the repository or open an issue for support or feature requests.