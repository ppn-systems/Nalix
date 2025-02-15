namespace Notio.Logging.Targets;

using Notio.Common.Logging;
using Notio.Common.Models;
using Notio.Logging.Formatters;
using Notio.Logging.Internal.File;
using System;

/// <summary>
/// Standard file logger implementation that writes log messages to a file.
/// </summary>
/// <remarks>
/// This logger uses a specified formatter to format the log message before writing it to a file.
/// The default behavior can be customized by providing a custom <see cref="ILoggingFormatter"/>.
/// </remarks>
public class FileLoggingTarget(ILoggingFormatter loggerFormatter, string directory, string filename) : ILoggingTarget, IDisposable
{
    /// <summary>
    /// The provider responsible for writing logs to a file.
    /// </summary>
    public readonly FileLoggerProvider LoggerPrv = new(directory, filename);

    private readonly ILoggingFormatter _loggerFormatter = loggerFormatter;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileLoggingTarget"/> with default log message formatting.
    /// </summary>
    /// <param name="directory">The directory where the log file will be stored.</param>
    /// <param name="filename">The name of the log file.</param>
    public FileLoggingTarget(string directory, string filename) : this(new LoggingFormatter(false), directory, filename)
    {
    }

    /// <summary>
    /// Publishes the formatted log entry to the log file.
    /// </summary>
    /// <param name="logMessage">The log entry to be published.</param>
    public void Publish(LoggingEntry logMessage)
        => LoggerPrv.WriteEntry(_loggerFormatter.FormatLog(logMessage));

    /// <summary>
    /// Disposes of the file logger and any resources it uses.
    /// </summary>
    public void Dispose()
    {
        LoggerPrv?.Dispose();
        GC.SuppressFinalize(this);
    }
}
