# Logging Extensions

This page covers the public `ILogger` convenience extensions in `Nalix.Logging`.

## Source mapping

- `src/Nalix.Logging/Extensions/NLogixFx.cs`
- `src/Nalix.Logging/Extensions/NLogixFx.Internal.cs`
- `src/Nalix.Logging/Extensions/NLogixFx.Level.cs`

## Main type

- `NLogixFx`

`NLogixFx` adds string- and exception-based convenience extensions that publish through a shared static distributor.

## Basic usage

```csharp
using Nalix.Logging.Extensions;

"listener started".Info(typeof(SampleProtocol));
"high latency detected".Warn(typeof(SampleProtocol));
exception.Error(typeof(SampleProtocol), "dispatch-failed");
```

## Public methods

- `Log(this string message, string source, LogLevel messageType, object? extendedData = null, ...)`
- `Log(this string message, Type source, LogLevel messageType, object? extendedData = null, ...)`
- `Log(this Exception ex, string? source = null, string? message = null, ...)`
- `Log(this Exception ex, Type? source = null, string? message = null, ...)`
- `Trace(this string message, string? source = null, object? extendedData = null, ...)`
- `Trace(this string message, Type source, object? extendedData = null, ...)`
- `Trace(this Exception extendedData, string source, string message, ...)`
- `Debug(this string message, string? source = null, object? extendedData = null, ...)`
- `Debug(this string message, Type source, object? extendedData = null, ...)`
- `Debug(this Exception extendedData, string source, string message, ...)`
- `Info(this string message, string? source = null, object? extendedData = null, ...)`
- `Info(this string message, Type source, object? extendedData = null, ...)`
- `Info(this Exception extendedData, string source, string message, ...)`
- `Warn(this string message, string? source = null, object? extendedData = null, ...)`
- `Warn(this string message, Type source, object? extendedData = null, ...)`
- `Warn(this Exception extendedData, string source, string message, ...)`
- `Error(this string message, string? source = null, object? extendedData = null, ...)`
- `Error(this string message, Type source, object? extendedData = null, ...)`
- `Error(this Exception ex, string source, string message, ...)`
- `Fatal(this string message, string? source = null, object? extendedData = null, ...)`
- `Fatal(this string message, Type source, object? extendedData = null, ...)`
- `Fatal(this Exception extendedData, string source, string message, ...)`

## What they do

Each helper captures caller metadata and forwards the message, source, and optional extended data into the shared `NLogixFx.Publisher`.

## Static properties

- `NLogixFx.MinimumLevel`: Gets or sets the minimum logging level. Messages below this level are not logged. Defaults to `LogLevel.Trace`.
- `NLogixFx.Publisher`: The global `NLogixDistributor` instance used for distributing log messages to registered targets.

## Related APIs

- [Logging](./index.md)
- [Logging Targets](./targets.md)
