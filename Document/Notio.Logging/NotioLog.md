# NotioLog Class Documentation

The NotioLog class provides methods for logging events with various levels of severity, including debug, info, warning, error, and critical levels. This singleton class is the central logging engine of the Notio.Logging library.

## Namespace

```csharp
using Notio.Logging;
```

## Class Signature

```csharp
public sealed class NotioLog : LoggingEngine, ILogger
```

## Properties

### `Instance`

- Type: `NotioLog`

- Description: Provides the singleton instance of the `NotioLog` class.

### `Empty`

- Type: `EventId`

- Description: Represents an empty `EventId` with a default value of 0.

### `Methods`

Initialize

```csharp
public void Initialize(Action<LoggingConfig>? configure = null)
```

- **Description**: Initializes the logging system with an optional configuration.
- **Parameters**:
  - `configure`: An action to configure `LoggingConfig`.
- **Exceptions**:
  - Throws `InvalidOperationException` if the logging system is already initialized.

### `ConfigureDefaults`

```csharp
public void ConfigureDefaults(Func<LoggingConfig, LoggingConfig> defaults)
```

- **Description**: Configures default logging settings.
- **Parameters**:
  - defaults: A function to configure and return the modified LoggingConfig.

### `Write`

```csharp
public void Write(LoggingLevel level, EventId eventId, string message)
public void Write(LoggingLevel level, EventId eventId, Exception exception)
public void Write(LoggingLevel level, EventId eventId, string message, Exception exception)
```

- **Description**: Logs a message or exception at the specified logging level.
- **Parameters**:
  - `level`: The logging level (`LoggingLevel`).
  - `eventId`: The event ID (`EventId`).
  - `message`: The log message (optional in some overloads).
  - `exception`: The `exception` to log (optional in some overloads).
- **Exceptions**:
  - Throws `ArgumentException` if the message is null or whitespace.
  - Throws `ArgumentNullException` if the exception is null.

### `Logging Helper Methods`

Info

```csharp
public void Info(string format, params object[] args)
public void Info(string message, EventId? eventId = null)
```

- **Description**: Logs information messages.

- **Parameters**:
  - `format`: A format string.
  - `args`: Parameters for the format string.
  - `message`: The log message.
  - `eventId`: An optional event ID.

Debug

```csharp
public void Debug(string message, EventId? eventId = null, [CallerMemberName] string memberName = "")
```

- **Description**: Logs debug messages with optional member name.
- **Parameters**:
  - `message`: The log message.
  - `eventId`: An optional event ID.
  - `memberName`: The name of the calling member (automatically provided).

Trace

```csharp
public void Trace(string message, EventId? eventId = null)
```

- **Description**: Logs trace messages.
- **Parameters**:
  - `message`: The log message.
  - `eventId`: An optional event ID.

### `Initialization with Default Configuration`

```csharp
NotioLog.Instance.Initialize(cfg =>
{
    cfg.ConfigureDefaults(defaults =>
        defaults.AddTarget(new ConsoleTarget())
               .AddTarget(new FileTarget("Logs", "Application"))
               .SetMinLevel(LoggingLevel.Debug)
    );
});
```

### `Logging Messages`

```csharp
var logger = NotioLog.Instance;
logger.Info("Application started.");
logger.Debug("Debug message.");
logger.Error(new Exception("Something went wrong!"));

NotioLog.Instance.Info("Application end.");
```

## Remarks

The NotioLog class is designed to be thread-safe and supports extensible logging targets. It provides convenient methods for logging messages with different levels of severity and supports optional configurations for greater flexibility.
