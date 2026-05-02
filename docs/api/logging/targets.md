# Logging Targets

This page covers the built-in public logging targets in `Nalix.Logging.Sinks`.

## Source mapping

- `src/Nalix.Logging/Sinks/BatchConsoleLogTarget.cs`
- `src/Nalix.Logging/Sinks/BatchFileLogTarget.cs`

## Main types

- `BatchConsoleLogTarget`
- `BatchFileLogTarget`

## BatchConsoleLogTarget

`BatchConsoleLogTarget` buffers log entries and flushes them to the console through an internal provider.

### Public surface that matters

- constructor with `ConsoleLogOptions?` and `INLogixFormatter?` (both optional)
- constructor with `Action<ConsoleLogOptions>` and optional `INLogixFormatter?`
- `Publish(DateTime timestampUtc, LogLevel logLevel, EventId eventId, string message, Exception? exception)`
- `Dispose()`
- counters: `WrittenCount`, `DroppedCount`

## BatchFileLogTarget

`BatchFileLogTarget` is the non-blocking file sink backed by a batched file provider.

## Basic usage

```csharp
var fileTarget = new BatchFileLogTarget(options =>
{
    options.LogFileName = "server.log";
    options.MaxFileSizeBytes = 10 * 1024 * 1024;
});

fileTarget.Publish(entry);
fileTarget.Dispose();
```

### Public surface that matters

- constructor with `FileLogOptions?` and `INLogixFormatter` (formatter is required)
- default constructor (uses `FileLogFormatter`)
- constructor with `Action<FileLogOptions>` (uses `FileLogFormatter`)
- `Publish(DateTime timestampUtc, LogLevel logLevel, EventId eventId, string message, Exception? exception)`
- `Dispose()`

## FileError

`FileError` is the context object used when file logging operations fail.

## Source mapping

- `src/Nalix.Logging/Exceptions/FileError.cs`

It carries:

- `Exception`
- `OriginalFilePath`
- `NewLogFileName`

Use this type when you want to surface or recover from file-target problems with more context than a bare exception.

## Typical integration

```csharp
using Microsoft.Extensions.Logging;
using Nalix.Logging;
using Nalix.Logging.Options;
using Nalix.Logging.Sinks;

var logger = new NLogix(cfg =>
{
    cfg.SetMinimumLevel(LogLevel.Debug)
       .RegisterTarget(new BatchConsoleLogTarget(options =>
       {
           options.BatchSize = 64;
           options.EnableColors = true;
       }))
       .RegisterTarget(new BatchFileLogTarget(options =>
       {
           options.LogFileName = "server.log";
           options.UsePerProcessSuffix = true;
       }));
});
```

## Related APIs

- [Logging](./index.md)
- [Logging Options](../options/network/options.md)
