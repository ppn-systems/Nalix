using Notio.Common.Logging;
using Notio.Common.Models;
using Notio.Logging.Format;
using Notio.Logging.Storage;
using System;

namespace Notio.Logging.Targets;

/// <summary>
/// Standard file logger implementation.
/// </summary>
public class FileTarget(ILoggingFormatter loggerFormatter, string directory, string filename) : ILoggingTarget
{
    public readonly FileLoggerProvider LoggerPrv = new(directory, filename);
    private readonly ILoggingFormatter _loggerFormatter = loggerFormatter;

    public FileTarget(string directory, string filename) : this(new LoggingFormatter(), directory, filename)
    {
    }

    public void Publish(LoggingEntry logMessage)
        => LoggerPrv.WriteEntry(_loggerFormatter.FormatLog(logMessage, DateTime.Now));
}