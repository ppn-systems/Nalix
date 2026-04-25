---
title: "Logging"
description: "Reference for NLogix, the global host accessor, the target distributor, and the built-in batched console and file sinks."
---

`Nalix.Logging` supplies the batched logger used throughout the Nalix stack. It integrates with `Microsoft.Extensions.Logging.ILogger` while keeping sink registration and delivery under Nalix's own control.

## Import Paths

```csharp
using Nalix.Logging;
using Nalix.Logging.Options;
using Nalix.Logging.Sinks;
```

## Source

- [NLogix.cs](/workspace/home/nalix/src/Nalix.Logging/NLogix.cs)
- [NLogix.Host.cs](/workspace/home/nalix/src/Nalix.Logging/NLogix.Host.cs)
- [NLogixDistributor.cs](/workspace/home/nalix/src/Nalix.Logging/NLogixDistributor.cs)
- [Sinks/BatchConsoleLogTarget.cs](/workspace/home/nalix/src/Nalix.Logging/Sinks/BatchConsoleLogTarget.cs)
- [Sinks/BatchFileLogTarget.cs](/workspace/home/nalix/src/Nalix.Logging/Sinks/BatchFileLogTarget.cs)
- [Options/NLogixOptions.cs](/workspace/home/nalix/src/Nalix.Logging/Options/NLogixOptions.cs)
- [Options/ConsoleLogOptions.cs](/workspace/home/nalix/src/Nalix.Logging/Options/ConsoleLogOptions.cs)
- [Options/FileLogOptions.cs](/workspace/home/nalix/src/Nalix.Logging/Options/FileLogOptions.cs)

## `NLogix`

```csharp
public sealed partial class NLogix : ILogger, IDisposable
{
    public NLogix(Action<NLogixOptions>? configureOptions = null);
    public void ConfigureOptions(Action<NLogixOptions> configureOptions);
    public bool IsEnabled(LogLevel logLevel);
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter);
    public void Publish(LogLevel level, EventId? eventId, string message, Exception? error = null);
    public void Dispose();
}
```

Example:

```csharp
NLogix logger = new(cfg =>
{
    cfg.SetMinimumLevel(LogLevel.Information)
       .RegisterTarget(new BatchConsoleLogTarget())
       .RegisterTarget(new BatchFileLogTarget());
});
```

## `NLogix.Host`

```csharp
public sealed class Host
{
    public static NLogix Instance { get; }
}
```

This gives you the lazily initialized singleton logger.

Example:

```csharp
ILogger logger = NLogix.Host.Instance;
```

## `NLogixDistributor`

```csharp
public sealed class NLogixDistributor : INLogixDistributor
{
    public long TotalPublishErrors { get; }
    public long TotalEntriesPublished { get; }
    public long TotalTargetInvocations { get; }
    public void Publish(DateTime timestampUtc, LogLevel logLevel, EventId eventId, string message, Exception? exception);
    public INLogixDistributor RegisterTarget(INLogixTarget loggerHandler);
    public bool UnregisterTarget(INLogixTarget loggerHandler);
    public void Dispose();
    public override string ToString();
}
```

It fans one log entry out to all currently registered targets.

## Built-in Targets

### `BatchConsoleLogTarget`

```csharp
public sealed class BatchConsoleLogTarget : INLogixTarget, IDisposable
{
    public long WrittenCount { get; }
    public long DroppedCount { get; }
    public BatchConsoleLogTarget(ConsoleLogOptions? options = null, INLogixFormatter? formatter = null);
    public BatchConsoleLogTarget(Action<ConsoleLogOptions> options, INLogixFormatter? formatter = null);
    public void Publish(DateTime timestampUtc, LogLevel logLevel, EventId eventId, string message, Exception? exception);
    public void Dispose();
}
```

### `BatchFileLogTarget`

```csharp
public sealed class BatchFileLogTarget : INLogixTarget, IDisposable
{
    public BatchFileLogTarget(FileLogOptions? options, INLogixFormatter formatter);
    public BatchFileLogTarget();
    public BatchFileLogTarget(Action<FileLogOptions> options);
    public void Publish(DateTime timestampUtc, LogLevel logLevel, EventId eventId, string message, Exception? exception);
    public void Dispose();
}
```

## Options

### `NLogixOptions`

| Property | Type | Default | Description |
|---|---|---|---|
| `MinLevel` | `LogLevel` | `Information` | Minimum enabled log level. |
| `FileOptions` | `FileLogOptions` | new instance | Embedded file target options. |
| `TimestampFormat` | `string` | `"yyyy-MM-dd HH:mm:ss.fff"` | Log timestamp format. |
| `UseUtcTimestamp` | `bool` | `true` | Uses UTC timestamps. |
| `IncludeProcessId` | `bool` | `true` | Includes process ID. |
| `IncludeTimestamp` | `bool` | `true` | Includes a timestamp in each entry. |
| `IncludeMachineName` | `bool` | `true` | Includes machine name. |
| `GroupConcurrencyLimit` | `int` | `3` | Concurrent log processing limit per target. |

### `ConsoleLogOptions`

| Property | Type | Default | Description |
|---|---|---|---|
| `BatchSize` | `int` | `32` | Entries per flush batch. |
| `MaxQueueSize` | `int` | `0` | Queue limit; `0` means unlimited. |
| `AdaptiveFlush` | `bool` | `true` | Adjusts flush timing based on load. |
| `BlockWhenQueueFull` | `bool` | `false` | Blocks instead of dropping entries. |
| `EnableFlush` | `bool` | `false` | Flushes console after each batch. |
| `EnableColors` | `bool` | `true` | Enables ANSI colors. |
| `BatchDelay` | `TimeSpan` | `00:00:00.070` | Delay between flushes. |

### `FileLogOptions`

| Property | Type | Default | Description |
|---|---|---|---|
| `MaxFileSizeBytes` | `int` | `10485760` | Rotation threshold. |
| `MaxQueueSize` | `int` | `4096` | File writer queue bound. |
| `LogFileName` | `string` | `log_{MachineName}_.log` | Base file name template. |
| `FlushInterval` | `TimeSpan` | `00:00:01` | Disk flush interval. |
| `BlockWhenQueueFull` | `bool` | `false` | Blocks when queue is full. |
| `UsePerProcessSuffix` | `bool` | `false` | Adds process name and ID to the file name. |
| `FormatLogFileName` | `Func<string, string>?` | runtime | Custom file naming hook. |
| `HandleFileError` | `Action<FileError>?` | runtime | Error callback for file operations. |

## Usage Pattern

```csharp
ILogger logger = new NLogix(cfg =>
{
    cfg.SetMinimumLevel(LogLevel.Debug)
       .RegisterTarget(new BatchConsoleLogTarget())
       .RegisterTarget(new BatchFileLogTarget(options =>
       {
           options.LogFileName = "server.log";
           options.UsePerProcessSuffix = true;
       }));
});
```

## Related Types

- [Network Builder](/docs/api-reference/network-builder)
