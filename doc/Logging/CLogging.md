# CLogging Class Documentation

The `CLogging` class is a singleton that provides comprehensive logging functionality for the application. It is part of the `Notio.Logging` namespace and offers various logging levels and methods to log messages and exceptions.

## Namespace

```csharp
using Notio.Common.Logging;
using Notio.Common.Models;
using Notio.Logging.Core;
using Notio.Logging.Targets;
using System;
using System.Runtime.CompilerServices;
```

## Class Definition

### Summary

The `CLogging` class is designed to be a robust and flexible logging system, allowing for detailed and structured logging throughout the application. It initializes the logging system with optional configuration and supports multiple logging targets.

```csharp
namespace Notio.Logging
{
    /// <summary>
    /// A singleton class that provides logging functionality for the application.
    /// </summary>
    /// <remarks>
    /// Initializes the logging system with optional configuration.
    /// </remarks>
    /// <param name="configure">An optional action to configure the logging system.</param>
    public sealed class CLogging(Action<LoggingOptions>? configure = null) : LoggingEngine(configure), ILogger
    {
        // Class implementation...
    }
}
```

## Properties

### Instance

```csharp
public static CLogging Instance { get; }
```

- **Description**: Gets the single instance of the `CLogging` class.
- **Initialization**: Configures the logging system with console and file logging targets by default.

## Methods

### Meta

```csharp
public void Meta(string message, EventId? eventId = null)
```

- **Description**: Logs a message with `Meta` level.
- **Parameters**:
  - `message`: The message to log.
  - `eventId` (optional): The event ID to associate with the log entry.

### Trace

```csharp
public void Trace(string message, EventId? eventId = null)
```

- **Description**: Logs a message with `Trace` level.
- **Parameters**:
  - `message`: The message to log.
  - `eventId` (optional): The event ID to associate with the log entry.

### Debug

```csharp
public void Debug(string message, EventId? eventId = null, [CallerMemberName] string memberName = "")
```

- **Description**: Logs a message with `Debug` level.
- **Parameters**:
  - `message`: The message to log.
  - `eventId` (optional): The event ID to associate with the log entry.
  - `memberName`: The name of the member invoking the log (automatically captured).

### Debug<`TClass`>

```csharp
public void Debug<TClass>(string message, EventId? eventId = null, [CallerMemberName] string memberName = "")
    where TClass : class
```

- **Description**: Logs a message with `Debug` level, including the class name and member name.
- **Parameters**:
  - `message`: The message to log.
  - `eventId` (optional): The event ID to associate with the log entry.
  - `memberName`: The name of the member invoking the log (automatically captured).

### Info

```csharp
public void Info(string format, params object[] args)
```

- **Description**: Logs a formatted message with `Info` level.
- **Parameters**:
  - `format`: The format string.
  - `args`: The arguments to format.

```csharp
public void Info(string message, EventId? eventId = null)
```

- **Description**: Logs a message with `Info` level.
- **Parameters**:
  - `message`: The message to log.
  - `eventId` (optional): The event ID to associate with the log entry.

### Warn

```csharp
public void Warn(string message, EventId? eventId = null)
```

- **Description**: Logs a message with `Warn` level.
- **Parameters**:
  - `message`: The message to log.
  - `eventId` (optional): The event ID to associate with the log entry.

### Error

```csharp
public void Error(Exception exception, EventId? eventId = null)
```

- **Description**: Logs an exception with `Error` level.
- **Parameters**:
  - `exception`: The exception to log.
  - `eventId` (optional): The event ID to associate with the log entry.

```csharp
public void Error(string message, Exception exception, EventId? eventId = null)
```

- **Description**: Logs a message and an exception with `Error` level.
- **Parameters**:
  - `message`: The message to log.
  - `exception`: The exception to log.
  - `eventId` (optional): The event ID to associate with the log entry.

```csharp
public void Error(string message, EventId? eventId = null)
```

- **Description**: Logs a message with `Error` level.
- **Parameters**:
  - `message`: The message to log.
  - `eventId` (optional): The event ID to associate with the log entry.

### Fatal

```csharp
public void Fatal(string message, EventId? eventId = null)
```

- **Description**: Logs a message with `Fatal` level.
- **Parameters**:
  - `message`: The message to log.
  - `eventId` (optional): The event ID to associate with the log entry.

```csharp
public void Fatal(string message, Exception exception, EventId? eventId = null)
```

- **Description**: Logs a message and an exception with `Fatal` level.
- **Parameters**:
  - `message`: The message to log.
  - `exception`: The exception to log.
  - `eventId` (optional): The event ID to associate with the log entry.

## Private Methods

### WriteLog

```csharp
private void WriteLog(LoggingLevel level, EventId eventId, string message, Exception? exception = null)
```

- **Description**: Writes a log entry with the specified level, event ID, message, and optional exception.
- **Parameters**:
  - `level`: The log level (e.g., Info, Warning, Error, etc.).
  - `eventId`: The event ID to associate with the log entry.
  - `message`: The log message.
  - `exception` (optional): The exception associated with the log entry.

### SanitizeLogMessage

```csharp
private static string SanitizeLogMessage(string? message)
```

- **Description**: Sanitizes the log message to prevent log forging by removing potentially dangerous characters (e.g., newlines or control characters).
- **Parameters**:
  - `message`: The message to sanitize.
- **Returns**: A sanitized log message.

## Remarks

The `CLogging` class is designed to be easily configurable and extendable. By default, it uses console and file logging targets, but additional targets can be added through configuration.

```csharp
public static CLogging Instance { get; } = new(cfg => cfg
    .AddTarget(new ConsoleLoggingTarget())
    .AddTarget(new FileLoggingTarget())
);
```
