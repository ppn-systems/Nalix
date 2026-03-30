# Logging Extensions

This page covers the public `ILogger` convenience extensions in `Nalix.Logging`.

## Source mapping

- `src/Nalix.Logging/NLogix.Extensions.cs`

## Main type

- `NLogixExtensions`

`NLogixExtensions` adds contextual logging helpers such as `Info<T>(...)` and `Error<T>(...)`.

## Basic usage

```csharp
logger.Info<SampleProtocol>("listener started");
logger.Warn<SampleProtocol>("high latency detected");
logger.Error<SampleProtocol>(exception);
```

## Public methods

- `Trace<T>(logger, message, eventId, member)`
- `Debug<T>(logger, message, eventId, member)`
- `Info<T>(logger, message, eventId, member)`
- `Warn<T>(logger, message, eventId, member)`
- `Error<T>(logger, message, eventId, member)`
- `Error<T>(logger, exception, eventId, member)`
- `Fatal<T>(logger, message, eventId, member)`
- `Fatal<T>(logger, exception, eventId, member)`

## What they do

Each helper prefixes the log message with:

```text
[TypeName:MemberName]
```

That makes logs easier to scan without formatting the prefix manually at every call site.

## Related APIs

- [Logging](./index.md)
- [Logging Targets](./targets.md)
