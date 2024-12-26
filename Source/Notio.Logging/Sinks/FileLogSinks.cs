using Notio.Logging.Extensions;
using Notio.Logging.Format;
using Notio.Logging.Interfaces;
using Notio.Logging.Metadata;
using System;

namespace Notio.Logging.Sinks;

/// <summary>
/// Trình ghi tệp chung hoạt động theo cách tiêu chuẩn.
/// </summary>
public class FileLogSinks(ILoggingFormatter loggerFormatter) : ILoggingSinks
{
    private readonly ILoggingFormatter _loggerFormatter = loggerFormatter;

    public readonly FileLoggerProvider LoggerPrv = new("Notio_");

    public FileLogSinks() : this(new LoggingFormatter()) { }

    public void Publish(LogMessage logMessage)
        => LoggerPrv.WriteEntry(_loggerFormatter.FormatLog(logMessage, DateTime.Now));
}