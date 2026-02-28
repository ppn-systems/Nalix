# Nalix.Logging

High-performance logging system optimized for low-latency networking environments.

## Features

- **NLogix**: A zero-allocation logger built for hot-path networking execution.
- **Batched Sinks**: Prevents logging from blocking network throughput by using asynchronous batching.
- **Modular Targets**: Support for Console, File, and external collector targets.
- **Diagnostic API**: Built-in support for statistics and metrics reporting.

## Installation

```bash
dotnet add package Nalix.Logging
```

## Quick Example: Usage

```csharp
ILogger logger = NLogix.Host.Instance;
logger.LogInformation("Nalix system initialized successfully.");
```

## Documentation

See [Logging Targets](https://ppn-systems.me/api/logging/targets) for a list of available sinks.
