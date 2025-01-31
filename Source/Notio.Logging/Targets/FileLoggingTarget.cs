using Notio.Common.Logging.Interfaces;
using Notio.Common.Models;
using Notio.Logging.Formatters;
using Notio.Logging.Targets.File;

namespace Notio.Logging.Targets;

/// <summary>
/// Standard file logger implementation.
/// </summary>
public class FileLoggingTarget(ILoggingFormatter loggerFormatter, string directory, string filename) : ILoggingTarget
{
    public readonly FileLoggerProvider LoggerPrv = new(directory, filename);
    private readonly ILoggingFormatter _loggerFormatter = loggerFormatter;

    public FileLoggingTarget(string directory, string filename) : this(new LoggingFormatter(false), directory, filename)
    {
    }

    public void Publish(LoggingEntry logMessage)
        => LoggerPrv.WriteEntry(_loggerFormatter.FormatLog(logMessage));
}