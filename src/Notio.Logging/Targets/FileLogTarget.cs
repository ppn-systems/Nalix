using Notio.Common.Logging;
using Notio.Logging.Engine.Formatters;
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

public sealed class FileLogTarget : ILoggerTarget, IDisposable
{
    #region Fields

    private readonly FileLoggerProvider _loggerPrv;
    private readonly ILoggerFormatter _loggerFormatter;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="FileLogTarget"/> class.
    /// </summary>
    /// <param name="loggerFormatter">The log message formatter.</param>
    /// <param name="fileLoggerOptions">The file logger options.</param>
    public FileLogTarget(ILoggerFormatter loggerFormatter, FileLogOptions fileLoggerOptions)
    {
        ArgumentNullException.ThrowIfNull(loggerFormatter);
        ArgumentNullException.ThrowIfNull(fileLoggerOptions);

        _loggerFormatter = loggerFormatter;
        _loggerPrv = new FileLoggerProvider(fileLoggerOptions);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileLogTarget"/> with default log message formatting.
    /// </summary>
    public FileLogTarget()
        : this(new LoggingFormatter(false), new FileLogOptions())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileLogTarget"/> with default log message formatting.
    /// </summary>
    /// <param name="options">A delegate to configure <see cref="FileLogOptions"/>.</param>
    public FileLogTarget(FileLogOptions options)
        : this(new LoggingFormatter(false), options)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileLogTarget"/> with configurable log message formatting.
    /// </summary>
    /// <param name="configureOptions">A delegate to configure <see cref="FileLogOptions"/>.</param>
    public FileLogTarget(Action<FileLogOptions> configureOptions)
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
    private static FileLogOptions ConfigureOptions(Action<FileLogOptions> configureOptions)
    {
        FileLogOptions options = new();
        configureOptions?.Invoke(options);
        return options;
    }

    #endregion
}
