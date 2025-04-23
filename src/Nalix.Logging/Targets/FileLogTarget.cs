using Nalix.Common.Logging;
using Nalix.Logging.Engine.Formatters;
using Nalix.Logging.Formatters.Internal;
using Nalix.Logging.Options;
using System;

namespace Nalix.Logging.Targets;

/// <summary>
/// Provides a standard implementation for logging messages to a file.
/// This class writes log messages to a specified file, with support for custom formatting.
/// </summary>
/// <remarks>
/// The file logger uses an <see cref="ILoggerFormatter"/> to format log entries before writing them to the file.
/// By default, a simple logging formatter is used, but you can configure a custom formatter if needed.
/// The file logging behavior can also be customized through <see cref="FileLogOptions"/>.
/// </remarks>
public sealed class FileLogTarget : ILoggerTarget, IDisposable
{
    #region Fields

    private readonly FileLoggerProvider _loggerPrv;
    private readonly ILoggerFormatter _loggerFormatter;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="FileLogTarget"/> class with a custom log formatter and options.
    /// </summary>
    /// <param name="loggerFormatter">The formatter used to format log messages.</param>
    /// <param name="fileLoggerOptions">The options that configure the file logger's behavior.</param>
    /// <exception cref="ArgumentNullException">Thrown when either <paramref name="loggerFormatter"/> or <paramref name="fileLoggerOptions"/> is <c>null</c>.</exception>
    public FileLogTarget(ILoggerFormatter loggerFormatter, FileLogOptions fileLoggerOptions)
    {
        ArgumentNullException.ThrowIfNull(loggerFormatter);
        ArgumentNullException.ThrowIfNull(fileLoggerOptions);

        _loggerFormatter = loggerFormatter;
        _loggerPrv = new FileLoggerProvider(fileLoggerOptions);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileLogTarget"/> class with the default formatter and options.
    /// </summary>
    public FileLogTarget()
        : this(new LoggingFormatter(false), new FileLogOptions())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileLogTarget"/> class with default formatting and provided file logger options.
    /// </summary>
    /// <param name="options">A delegate to configure <see cref="FileLogOptions"/>.</param>
    public FileLogTarget(FileLogOptions options)
        : this(new LoggingFormatter(false), options)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileLogTarget"/> class with configurable formatting and file logger options.
    /// </summary>
    /// <param name="configureOptions">A delegate to configure <see cref="FileLogOptions"/>.</param>
    public FileLogTarget(Action<FileLogOptions> configureOptions)
        : this(new LoggingFormatter(false), ConfigureOptions(configureOptions))
    {
    }

    #endregion Constructors

    #region Public Methods

    /// <summary>
    /// Publishes the formatted log entry to the log file.
    /// </summary>
    /// <param name="logMessage">The log entry to be published.</param>
    public void Publish(LogEntry logMessage)
        => _loggerPrv.WriteEntry(_loggerFormatter.FormatLog(logMessage));

    #endregion Public Methods

    #region IDisposable

    /// <summary>
    /// Disposes of the file logger and releases any associated resources.
    /// This method flushes the log queue and disposes of the logger provider.
    /// </summary>
    public void Dispose()
    {
        _loggerPrv.FlushQueue();
        _loggerPrv.Dispose();
        GC.SuppressFinalize(this);
    }

    #endregion IDisposable

    #region Private Methods

    /// <summary>
    /// Helper method to apply configuration to <see cref="FileLogOptions"/>.
    /// </summary>
    /// <param name="configureOptions">A delegate that configures <see cref="FileLogOptions"/>.</param>
    /// <returns>Configured <see cref="FileLogOptions"/> instance.</returns>
    private static FileLogOptions ConfigureOptions(Action<FileLogOptions> configureOptions)
    {
        FileLogOptions options = new();
        configureOptions?.Invoke(options);
        return options;
    }

    #endregion Private Methods
}
