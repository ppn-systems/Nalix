using Notio.Logging.Extensions;
using Notio.Logging.Format;
using Notio.Logging.Interfaces;
using Notio.Logging.Metadata;
using System;
using System.IO;

namespace Notio.Logging.Targets;

/// <summary>
/// Standard file logger implementation.
/// </summary>
public class FileTarget : ILoggingTarget
{
    public readonly FileLoggerProvider LoggerPrv;
    private readonly ILoggingFormatter _loggerFormatter;

    public FileTarget(string directory, string filename) : this(new LoggingFormatter(), directory, filename) { }

    public FileTarget(ILoggingFormatter loggerFormatter, string directory, string filename)
    {
        _loggerFormatter = loggerFormatter;
        LoggerPrv = new FileLoggerProvider(directory, filename);
    }

    public void Publish(LoggingEntry logMessage)
        => LoggerPrv.WriteEntry(_loggerFormatter.FormatLog(logMessage, DateTime.Now));
}
