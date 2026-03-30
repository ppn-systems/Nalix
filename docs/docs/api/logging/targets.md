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

## Basic usage

```csharp
var consoleTarget = new BatchConsoleLogTarget(options =>
{
    options.BatchSize = 64;
    options.EnableColors = true;
});

consoleTarget.Publish(entry);
await consoleTarget.WriteAsync(entry);
```

### Public surface that matters

- constructor with `ConsoleLogOptions`
- constructor with `Action<ConsoleLogOptions>`
- `Publish(LogEntry entry)`
- `WriteAsync(LogEntry entry)`
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

- constructor with `FileLogOptions` and `ILoggerFormatter`
- default constructor
- constructor with `Action<FileLogOptions>`
- `Publish(LogEntry entry)`
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
var logger = new NLogix(cfg =>
{
    cfg.RegisterTarget(new BatchConsoleLogTarget());
    cfg.RegisterTarget(new BatchFileLogTarget());
});
```

## Related APIs

- [Logging](./index.md)
- [Logging Options](./options.md)
