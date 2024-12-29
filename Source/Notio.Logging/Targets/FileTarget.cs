using Notio.Logging.Extensions;
using Notio.Logging.Format;
using Notio.Logging.Interfaces;
using Notio.Logging.Metadata;
using System;

namespace Notio.Logging.Targets;

/// <summary>
/// Trình ghi tệp chung hoạt động theo cách tiêu chuẩn.
/// </summary>
public class FileTarget(ILoggingFormatter loggerFormatter) : ILoggingTarget
{
    private readonly ILoggingFormatter _loggerFormatter = loggerFormatter;

    public readonly FileLoggerProvider LoggerPrv = new("Notio");

    public FileTarget() : this(new LoggingFormatter())
    {
    }

    public void Publish(LogEntry logMessage)
        => LoggerPrv.WriteEntry(_loggerFormatter.FormatLog(logMessage, DateTime.Now));
}