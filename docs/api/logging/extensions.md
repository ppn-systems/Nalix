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
- `Debug(this string message, string? source = null, object? extendedData = null, ...)`
- `Info(this string message, string? source = null, object? extendedData = null, ...)`
- `Warn(this string message, string? source = null, object? extendedData = null, ...)`
- `Error(this string message, string? source = null, object? extendedData = null, ...)`
- `Fatal(this string message, string? source = null, object? extendedData = null, ...)`

## What they do

Each helper captures caller metadata and forwards the message, source, and optional extended data into the shared `NLogixFx.Publisher`.

## Related APIs

- [Logging](./index.md)
- [Logging Targets](./targets.md)
