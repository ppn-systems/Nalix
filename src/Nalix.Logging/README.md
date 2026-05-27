# Nalix.Logging

> ⚠️ **WARNING: DEVELOPMENT USE ONLY**
> This logging system is designed **strictly for development environments**.
> It is NOT intended for production use. For production environments, please switch to a robust, enterprise-grade logging library such as [Serilog](https://serilog.net/), [NLog](https://nlog-project.org/), or use [OpenTelemetry](https://opentelemetry.io/).

## Key Features

| Feature | Description |
| :--- | :--- |
| ⚡ **NLogix** | Lightweight logger built for local execution and debugging. |
| 📦 **Batched Sinks** | Asynchronous batching to prevent logging from blocking throughput during testing. |
| 🔌 **Modular Targets** | Basic support for Console and File targets for local development. |
| 🛑 **Dev-Only** | Simplified, unbloated pipeline optimized for the developer experience. |

## Installation

```bash
dotnet add package Nalix.Logging
```

## Quick Example

```csharp
using Microsoft.Extensions.Logging;
using Nalix.Logging;

// WARNING: Use only in development!
ILogger logger = NLogix.Host.Instance;
logger.LogInformation("Nalix system initialized successfully in DEV mode.");
```

## Documentation

See [Logging Targets](https://ppn-system.me/api/logging/targets) for a list of available sinks and configuration options.
