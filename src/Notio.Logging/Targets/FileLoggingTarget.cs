using Notio.Common.Logging;
using Notio.Logging.Core.Formatters;
using Notio.Logging.Internal.Files;
using Notio.Logging.Options;
using System;

namespace Notio.Logging.Targets;

/// <summary>
/// Standard file logger implementation that writes log messages to a file.
/// </summary>
/// <remarks>
/// This logger uses a specified formatter to format the log message before writing it to a file.
/// The default behavior can be customized by providing a custom <see cref="ILoggerFormatter"/>.
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="FileLoggingTarget"/> class.
/// </remarks>
/// <param name="loggerFormatter">The log message formatter.</param>
/// <param name="fileLoggerOptions">The file logger options.</param>
public sealed class FileLoggingTarget(ILoggerFormatter loggerFormatter, FileLoggerOptions fileLoggerOptions)
    : ILoggerTarget, IDisposable
{
    private readonly ILoggerFormatter _loggerFormatter = loggerFormatter ??
        throw new ArgumentNullException(nameof(loggerFormatter));

    /// <summary>
    /// The provider responsible for writing logs to a file.
    /// </summary>
    private readonly FileLoggerProvider _loggerPrv = new(fileLoggerOptions);

    /// <summary>
    /// Initializes a new instance of the <see cref="FileLoggingTarget"/> with default log message formatting.
    /// </summary>
    public FileLoggingTarget()
        : this(new LoggingFormatter(), new FileLoggerOptions())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileLoggingTarget"/> with default log message formatting.
    /// </summary>
    /// <param name="options">A delegate to configure <see cref="FileLoggerOptions"/>.</param>
    public FileLoggingTarget(FileLoggerOptions options)
        : this(new LoggingFormatter(), options)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileLoggingTarget"/> with configurable log message formatting.
    /// </summary>
    /// <param name="configureOptions">A delegate to configure <see cref="FileLoggerOptions"/>.</param>
    public FileLoggingTarget(Action<FileLoggerOptions> configureOptions)
        : this(new LoggingFormatter(), ConfigureOptions(configureOptions))
    {
    }

    /// <summary>
    /// Publishes the formatted log entry to the log file.
    /// </summary>
    /// <param name="logMessage">The log entry to be published.</param>
    public void Publish(LogEntry logMessage)
        => _loggerPrv.WriteEntry(_loggerFormatter.FormatLog(logMessage));

    /// <summary>
    /// Disposes of the file logger and any resources it uses.
    /// </summary>
    public void Dispose()
    {
        _loggerPrv.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Helper method to apply configuration.
    /// </summary>
    private static FileLoggerOptions ConfigureOptions(Action<FileLoggerOptions> configureOptions)
    {
        var options = new FileLoggerOptions();
        configureOptions?.Invoke(options);
        return options;
    }
}
