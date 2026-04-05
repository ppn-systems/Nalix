# Nalix.Logging

`Nalix.Logging` provides the built-in structured logging implementation for the Nalix stack, including logger bootstrap, option models, and built-in targets.

## Install

```bash
dotnet add package Nalix.Logging
```

## What it includes

- `NLogix` and `NLogix.Host` for application-wide logging
- Shared `ILogger` integration
- Logging options for console and file targets
- Built-in batching and output target abstractions

## Typical use

Add this package when you want the default Nalix logger and its configuration model inside servers, workers, or shared runtime services.

## Documentation

- Package docs: [Nalix.Logging](https://ppn-systems.github.io/Nalix/packages/nalix-logging/)
- API docs: [Logging API](https://ppn-systems.github.io/Nalix/api/logging/)
