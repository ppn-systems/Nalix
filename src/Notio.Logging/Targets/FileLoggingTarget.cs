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

public sealed class FileLoggingTarget : ILoggerTarget, IDisposable
{
    #region Fields

    private readonly FileLoggerProvider _loggerPrv;
    private readonly ILoggerFormatter _loggerFormatter;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="FileLoggingTarget"/> class.
    /// </summary>
    /// <param name="loggerFormatter">The log message formatter.</param>
    /// <param name="fileLoggerOptions">The file logger options.</param>
    public FileLoggingTarget(ILoggerFormatter loggerFormatter, FileLoggerOptions fileLoggerOptions)
    {
        ArgumentNullException.ThrowIfNull(loggerFormatter);
        ArgumentNullException.ThrowIfNull(fileLoggerOptions);

        _loggerFormatter = loggerFormatter;
        _loggerPrv = new FileLoggerProvider(fileLoggerOptions);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileLoggingTarget"/> with default log message formatting.
    /// </summary>
    public FileLoggingTarget()
        : this(new LoggingFormatter(false), new FileLoggerOptions())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileLoggingTarget"/> with default log message formatting.
    /// </summary>
    /// <param name="options">A delegate to configure <see cref="FileLoggerOptions"/>.</param>
    public FileLoggingTarget(FileLoggerOptions options)
        : this(new LoggingFormatter(false), options)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileLoggingTarget"/> with configurable log message formatting.
    /// </summary>
    /// <param name="configureOptions">A delegate to configure <see cref="FileLoggerOptions"/>.</param>
    public FileLoggingTarget(Action<FileLoggerOptions> configureOptions)
        : this(new LoggingFormatter(false), ConfigureOptions(configureOptions))
    {
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Publishes the formatted log entry to the log file.
    /// </summary>
    /// <param name="logMessage">The log entry to be published.</param>
    public void Publish(LogEntry logMessage)
        => _loggerPrv.WriteEntry(_loggerFormatter.FormatLog(logMessage));

    #endregion

    #region IDisposable

    /// <summary>
    /// Disposes of the file logger and any resources it uses.
    /// </summary>
    public void Dispose()
    {
        _loggerPrv.FlushQueue();
        _loggerPrv.Dispose();
        GC.SuppressFinalize(this);
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Helper method to apply configuration.
    /// </summary>
    private static FileLoggerOptions ConfigureOptions(Action<FileLoggerOptions> configureOptions)
    {
        FileLoggerOptions options = new();
        configureOptions?.Invoke(options);
        return options;
    }

    #endregion
}
