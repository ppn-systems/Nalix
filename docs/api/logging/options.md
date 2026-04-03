# Logging Options

Nalix.Logging exposes a small set of public option classes for logger configuration.

## Source mapping

- `src/Nalix.Logging/Options/NLogixOptions.cs`
- `src/Nalix.Logging/Options/FileLogOptions.cs`
- `src/Nalix.Logging/Options/ConsoleLogOptions.cs`

## Main types

- `NLogixOptions`
- `FileLogOptions`
- `ConsoleLogOptions`

## NLogixOptions

`NLogixOptions` controls top-level logger behavior such as:

- minimum log level
- timestamp format
- UTC vs local timestamps
- process and machine metadata
- target registration

## Basic usage

```csharp
var options = new NLogixOptions()
    .SetMinimumLevel(LogLevel.Debug)
    .ConfigureFileOptions(f => f.LogFileName = "server.log");
```

## FileLogOptions

`FileLogOptions` controls file sink behavior such as:

- max file size
- queue size
- flush interval
- blocking vs dropping when full
- naming and per-process suffixes

## ConsoleLogOptions

`ConsoleLogOptions` controls console sink behavior such as:

- batch size
- queue size
- adaptive flush
- colors
- flush delay

## Related APIs

- [Logging](./index.md)
- [Configuration and DI](../framework/runtime/configuration.md)
