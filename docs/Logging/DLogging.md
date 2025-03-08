# DLogging Class Documentation

The `DLogging` class provides a centralized logging interface for the Notio framework. It includes various methods to log messages at different levels, with support for extended data and automatic caller information.

## Namespace

```csharp
using Notio.Common.Logging;
using Notio.Common.Models;
using Notio.Logging.Core;
using Notio.Logging.Internal.File;
using Notio.Logging.Targets;
using System;
using System.IO;
using System.Runtime.CompilerServices;
```

## Class Definition

### Summary

The `DLogging` class provides static methods to log messages at different levels (Debug, Trace, Info, Warn, Error, Fatal) and to configure logging settings. It uses a global logging publisher to distribute log messages to various targets.

```csharp
namespace Notio.Logging
{
    /// <summary>
    /// Provides a centralized logging interface for the Notio framework.
    /// </summary>
    public static class DLoggingExtensions
    {
        // Class implementation...
    }
}
```

## Properties

### Publisher

```csharp
public static readonly ILoggingPublisher Publisher;
```

- **Description**: The global logging publisher used for distributing log messages to various targets.

### MinimumLevel

```csharp
public static LoggingLevel MinimumLevel { get; set; } = LoggingLevel.Trace;
```

- **Description**: Gets or sets the minimum logging level. Messages below this level will not be logged.

## Static Constructor

### DLoggingExtensions()

```csharp
static DLoggingExtensions()
```

- **Description**: Initializes static members of the `DLogging` class. Configures the logging system with default targets and settings.

## Methods

### Debug (string, string?, object?, string, string, int)

```csharp
public static void Debug(
    this string message,
    string? source = null,
    object? extendedData = null,
    [CallerMemberName] string callerMemberName = "",
    [CallerFilePath] string callerFilePath = "",
    [CallerLineNumber] int callerLineNumber = 0)
```

- **Description**: Logs a debug message.
- **Parameters**:
  - `message`: The message to log.
  - `source`: The source of the message.
  - `extendedData`: Additional data to log with the message.
  - `callerMemberName`: The name of the caller member. Automatically populated.
  - `callerFilePath`: The file path of the caller. Automatically populated.
  - `callerLineNumber`: The line number in the caller file. Automatically populated.

### Debug (string, Type, object?, string, string, int)

```csharp
public static void Debug(
    this string message,
    Type source,
    object? extendedData = null,
    [CallerMemberName] string callerMemberName = "",
    [CallerFilePath] string callerFilePath = "",
    [CallerLineNumber] int callerLineNumber = 0)
```

- **Description**: Logs a debug message.
- **Parameters**:
  - `message`: The message to log.
  - `source`: The source type of the message.
  - `extendedData`: Additional data to log with the message.
  - `callerMemberName`: The name of the caller member. Automatically populated.
  - `callerFilePath`: The file path of the caller. Automatically populated.
  - `callerLineNumber`: The line number in the caller file. Automatically populated.

### Debug (Exception, string, string, string, string, int)

```csharp
public static void Debug(
    this Exception extendedData,
    string source,
    string message,
    [CallerMemberName] string callerMemberName = "",
    [CallerFilePath] string callerFilePath = "",
    [CallerLineNumber] int callerLineNumber = 0)
```

- **Description**: Logs a debug message with an exception.
- **Parameters**:
  - `extendedData`: The exception to log.
  - `source`: The source of the message.
  - `message`: The message to log.
  - `callerMemberName`: The name of the caller member. Automatically populated.
  - `callerFilePath`: The file path of the caller. Automatically populated.
  - `callerLineNumber`: The line number in the caller file. Automatically populated.

### Trace (string, string?, object?, string, string, int)

```csharp
public static void Trace(
    this string message,
    string? source = null,
    object? extendedData = null,
    [CallerMemberName] string callerMemberName = "",
    [CallerFilePath] string callerFilePath = "",
    [CallerLineNumber] int callerLineNumber = 0)
```

- **Description**: Logs a trace message.
- **Parameters**: Same as `Debug`.

### Trace (string, Type, object?, string, string, int)

```csharp
public static void Trace(
    this string message,
    Type source,
    object? extendedData = null,
    [CallerMemberName] string callerMemberName = "",
    [CallerFilePath] string callerFilePath = "",
    [CallerLineNumber] int callerLineNumber = 0)
```

- **Description**: Logs a trace message.
- **Parameters**: Same as `Debug`.

### Trace (Exception, string, string, string, string, int)

```csharp
public static void Trace(
    this Exception extendedData,
    string source,
    string message,
    [CallerMemberName] string callerMemberName = "",
    [CallerFilePath] string callerFilePath = "",
    [CallerLineNumber] int callerLineNumber = 0)
```

- **Description**: Logs a trace message with an exception.
- **Parameters**: Same as `Debug`.

### Warn (string, string?, object?, string, string, int)

```csharp
public static void Warn(
    this string message,
    string? source = null,
    object? extendedData = null,
    [CallerMemberName] string callerMemberName = "",
    [CallerFilePath] string callerFilePath = "",
    [CallerLineNumber] int callerLineNumber = 0)
```

- **Description**: Logs a warning message.
- **Parameters**: Same as `Debug`.

### Warn (string, Type, object?, string, string, int)

```csharp
public static void Warn(
    this string message,
    Type source,
    object? extendedData = null,
    [CallerMemberName] string callerMemberName = "",
    [CallerFilePath] string callerFilePath = "",
    [CallerLineNumber] int callerLineNumber = 0)
```

- **Description**: Logs a warning message.
- **Parameters**: Same as `Debug`.

### Warn (Exception, string, string, string, string, int)

```csharp
public static void Warn(
    this Exception extendedData,
    string source,
    string message,
    [CallerMemberName] string callerMemberName = "",
    [CallerFilePath] string callerFilePath = "",
    [CallerLineNumber] int callerLineNumber = 0)
```

- **Description**: Logs a warning message with an exception.
- **Parameters**: Same as `Debug`.

### Fatal (string, string?, object?, string, string, int)

```csharp
public static void Fatal(
    this string message,
    string? source = null,
    object? extendedData = null,
    [CallerMemberName] string callerMemberName = "",
    [CallerFilePath] string callerFilePath = "",
    [CallerLineNumber] int callerLineNumber = 0)
```

- **Description**: Logs a fatal error message.
- **Parameters**: Same as `Debug`.

### Fatal (string, Type, object?, string, string, int)

```csharp
public static void Fatal(
    this string message,
    Type source,
    object? extendedData = null,
    [CallerMemberName] string callerMemberName = "",
    [CallerFilePath] string callerFilePath = "",
    [CallerLineNumber] int callerLineNumber = 0)
```

- **Description**: Logs a fatal error message.
- **Parameters**: Same as `Debug`.

### Fatal (Exception, string, string, string, string, int)

```csharp
public static void Fatal(
    this Exception extendedData,
    string source,
    string message,
    [CallerMemberName] string callerMemberName = "",
    [CallerFilePath] string callerFilePath = "",
    [CallerLineNumber] int callerLineNumber = 0)
```

- **Description**: Logs a fatal error message with an exception.
- **Parameters**: Same as `Debug`.

### Info (string, string?, object?, string, string, int)

```csharp
public static void Info(
    this string message,
    string? source = null,
    object? extendedData = null,
    [CallerMemberName] string callerMemberName = "",
    [CallerFilePath] string callerFilePath = "",
    [CallerLineNumber] int callerLineNumber = 0)
```

- **Description**: Logs an info message.
- **Parameters**: Same as `Debug`.

### Info (string, Type, object?, string, string, int)

```csharp
public static void Info(
    this string message,
    Type source,
    object? extendedData = null,
    [CallerMemberName] string callerMemberName = "",
    [CallerFilePath] string callerFilePath = "",
    [CallerLineNumber] int callerLineNumber = 0)
```

- **Description**: Logs an info message.
- **Parameters**: Same as `Debug`.

### Info (Exception, string, string, string, string, int)

```csharp
public static void Info(
    this Exception extendedData,
    string source,
    string message,
    [CallerMemberName] string callerMemberName = "",
    [CallerFilePath] string callerFilePath = "",
    [CallerLineNumber] int callerLineNumber = 0)
```

- **Description**: Logs an info message with an exception.
- **Parameters**: Same as `Debug`.

### Error (string, string?, object?, string, string, int)

```csharp
public static void Error(
    this string message,
    string? source = null,
    object? extendedData = null,
    [CallerMemberName] string callerMemberName = "",
    [CallerFilePath] string callerFilePath = "",
    [CallerLineNumber] int callerLineNumber = 0)
```

- **Description**: Logs an error message.
- **Parameters**: Same as `Debug`.

### Error (string, Type, object?, string, string, int)

```csharp
public static void Error(
    this string message,
    Type source,
    object? extendedData = null,
    [CallerMemberName] string callerMemberName = "",
    [CallerFilePath] string callerFilePath = "",
    [CallerLineNumber] int callerLineNumber = 0)
```

- **Description**: Logs an error message.
- **Parameters**: Same as `Debug`.

### Error (Exception, string, string, string, string, int)

```csharp
public static void Error(
    this Exception ex,
    string source,
    string message,
    [CallerMemberName] string callerMemberName = "",
    [CallerFilePath] string callerFilePath = "",
    [CallerLineNumber] int callerLineNumber = 0)
```

- **Description**: Logs an error message with an exception.
- **Parameters**: Same as `Debug`.

### Log (string, string, LoggingLevel, object?, string, string, int)

```csharp
public static void Log(
    this string message,
    string source,
    LoggingLevel messageType,
    object? extendedData = null,
    [CallerMemberName] string callerMemberName = "",
    [CallerFilePath] string callerFilePath = "",
    [CallerLineNumber] int callerLineNumber = 0)
```

- **Description**: Logs a message at the specified logging level.
- **Parameters**:
  - `message`: The message to log.
  - `source`: The source of the message.
  - `messageType`: The logging level of the message.
  - `extendedData`: Additional data to log with the message.
  - `callerMemberName`: The name of the caller member. Automatically populated.
  - `callerFilePath`: The file path of the caller. Automatically populated.
  - `callerLineNumber`: The line number in the caller file. Automatically populated.

### Log (string, Type, LoggingLevel, object?, string, string, int)

```csharp
public static void Log(
    this string message,
    Type source,
    LoggingLevel messageType,
    object? extendedData = null,
    [CallerMemberName] string callerMemberName = "",
    [CallerFilePath] string callerFilePath = "",
    [CallerLineNumber] int callerLineNumber = 0)
```

- **Description**: Logs a message at the specified logging level.
- **Parameters**: Same as `Log`.

### Log (Exception, string?, string?, string, string, int)

```csharp
public static void Log(
    this Exception ex,
    string? source = null,
    string? message = null,
    [CallerMemberName] string callerMemberName = "",
    [CallerFilePath] string callerFilePath = "",
    [CallerLineNumber] int callerLineNumber = 0)
```

- **Description**: Logs an error message with an exception.
- **Parameters**: Same as `Error`.

### Log (Exception, Type?, string?, string, string, int)

```csharp
public static void Log(
    this Exception ex,
    Type? source = null,
    string? message = null,
    [CallerMemberName] string callerMemberName = "",
    [CallerFilePath] string callerFilePath = "",
    [CallerLineNumber] int callerLineNumber = 0)
```

- **Description**: Logs an error message with an exception.
- **Parameters**: Same as `Error`.

## Private Methods

### CreateLogEntry

```csharp
private static void CreateLogEntry(
    LoggingLevel level,
    string message,
    string? sourceName,
    object? extendedData,
    string callerMemberName,
    string callerFilePath,
    int callerLineNumber)
```

- **Description**: Creates a log entry.
- **Parameters**:
  - `level`: The logging level.
  - `message`: The message to log.
  - `sourceName`: The source of the message.
  - `extendedData`: Additional data to log with the message.
  - `callerMemberName`: The name of the caller member.
  - `callerFilePath`: The file path of the caller.
  - `callerLineNumber`: The line number in the caller file.

### BuildFullMessage

```csharp
private static string BuildFullMessage(
    string message,
    string? sourceName,
    object? extendedData,
    string callerMemberName,
    string callerFilePath,
    int callerLineNumber)
```

- **Description**: Builds the full log message.
- **Parameters**: Same as `CreateLogEntry`.
- **Returns**: The full log message as a string.

## Example Usage

Here's a basic example of how to use the `DLogging` class:

```csharp
using Notio.Logging;

public class LoggingExample
{
    public void LogInfo()
    {
        "This is an info message".Info();
    }

    public void LogError()
    {
        try
        {
            // Some code that throws an exception
        }
        catch (Exception ex)
        {
            ex.Error("LoggingExample", "An error occurred");
        }
    }
}
```

## Remarks

The `DLogging` class is designed to provide a flexible and centralized logging mechanism for the Notio framework. It supports various logging levels and allows for extended data and automatic caller information to be included in log messages.

Feel free to explore the individual methods and properties to understand their specific purposes and implementations. If you need detailed documentation for any specific file or directory, please refer to the source code or let me know!
