# Nalix.Logging

> High-performance logging system optimized for low-latency networking environments.

## Key Features

| Feature | Description |
| :--- | :--- |
| ⚡ **NLogix** | Zero-allocation logger built for hot-path networking execution. |
| 📦 **Batched Sinks** | Asynchronous batching prevents logging from blocking network throughput. |
| 🔌 **Modular Targets** | Support for Console, File, and external collector targets. |
| 📊 **Diagnostic API** | Built-in statistics and metrics reporting. |

## Installation

```bash
dotnet add package Nalix.Logging
```

## Quick Example

```csharp
using Nalix.Logging;

ILogger logger = NLogix.Host.Instance;
logger.LogInformation("Nalix system initialized successfully.");
```

## Documentation

See [Logging Targets](https://ppn-systems.me/api/logging/targets) for a list of available sinks and configuration options.
