# Nalix.Logging

> Lightweight, high-performance asynchronous logging engine for local development in the Nalix ecosystem.

> [!WARNING]
> **DEVELOPMENT AND TESTING ONLY**
> This logging system is designed strictly for local debugging, profiling, and development environments. It is **NOT** intended or optimized for high-volume enterprise production environments. For production workloads, please switch to a robust logging framework such as [Serilog](https://serilog.net/), [NLog](https://nlog-project.org/), or use [OpenTelemetry](https://opentelemetry.io/).

## Key Features

| Feature | Description | Key Concept / Type |
| :--- | :--- | :--- |
| ⚡ **NLogix Facade** | Extensible C# logging facade implementing Microsoft's `ILogger` interface with Aggressive Inlining optimizations. | `NLogix`, `NLogix.Host` |
| 📦 **Asynchronous Sinks** | High-efficiency channel-based distributor preventing logging operations from blocking hot code paths. | `NLogixDistributor`, `INLogixTarget` |
| 🔌 **Batched Outputs** | Native batched target execution writing console or local files in atomic, configured block increments. | `BatchConsoleLogTarget`, `BatchFileLogTarget` |
| 🎨 **ANSI Formatting** | Premium colorful console output engine with custom diagnostic level highlights and elapsed timestamps. | `AnsiColorFormatter`, `FileLogFormatter` |

## Key Namespaces

| Namespace | Purpose | Key Types |
| :--- | :--- | :--- |
| `Nalix.Logging` | Core root namespace providing the main logging facade, global singleton host, and asynchronous channel distributor | `NLogix`, `NLogix.Host`, `NLogixDistributor` |
| `Nalix.Logging.Abstractions` | System contracts defining custom targets, data formatting, pipelines, and local error handlers | `INLogixTarget`, `INLogixFormatter`, `INLogixDistributor`, `INLogixErrorHandler` |
| `Nalix.Logging.Sinks` | Pre-built asynchronous, channel-backed targets writing log lines to consoles or local files | `BatchConsoleLogTarget`, `BatchFileLogTarget` |
| `Nalix.Logging.Formatters` | Output formatting engines generating colored ANSI shell strings or structured file entries | `AnsiColorFormatter`, `FileLogFormatter` |
| `Nalix.Logging.Options` | Configuration schemas defining minimum log level, files rotation, paths, and sink behaviors | `NLogixOptions`, `ConsoleLogOptions`, `FileLogOptions` |
| `Nalix.Logging.Extensions` | Fluent API extension methods adding level-specific logging calls (`LogDebug`, `LogCritical`) | `NLogixFx`, `NLogixFx.Level` |
| `Nalix.Logging.Exceptions` | System error exceptions mapping filesystem and target write failures | `FileError` |

## Installation

```bash
dotnet add package Nalix.Logging
```

## Usage Examples

### 1. Global Singleton Usage (Quick Start)

The easiest way to use the logger in a development environment is via the global lazy-loaded `NLogix.Host.Instance` singleton.

```csharp
using Microsoft.Extensions.Logging;
using Nalix.Logging;

// Retrieve the pre-configured global developer instance
ILogger logger = NLogix.Host.Instance;

logger.LogInformation("Nalix host application started in DEVELOPMENT mode.");
logger.LogWarning("System diagnostic: active connections count is nearing limit.");
```

### 2. Advanced Pipeline Configuration (Custom Sinks)

You can spin up an isolated `NLogix` logging engine with highly customized batching configurations and log rotation targets.

```csharp
using System;
using Microsoft.Extensions.Logging;
using Nalix.Logging;
using Nalix.Logging.Options;
using Nalix.Logging.Sinks;

// Initialize a custom logging pipeline
using NLogix customLogger = new(options =>
{
    // 1. Set the global minimum log severity level
    options.SetMinimumLevel(LogLevel.Debug);

    // 2. Add a customized console sink with ANSI color formatting
    options.RegisterTarget(new BatchConsoleLogTarget());

    // 3. Register a specialized file logger writing to "logs/dev.log"
    options.RegisterTarget(new BatchFileLogTarget(fileOpts =>
    {
        fileOpts.FileName = "logs/dev.log";
        fileOpts.Append = true;
        fileOpts.AutoFlush = true;
        fileOpts.MaxSizeBytes = 5 * 1024 * 1024; // 5 MB rotate limit
    }));
});

// Write debug statements
customLogger.LogDebug("Custom asynchronous logging pipeline configured successfully.");
```

## Documentation

See [Nalix Logging API Reference](https://ppn-system.me/api/logging) for custom target sink implementation guides and formatter options.
