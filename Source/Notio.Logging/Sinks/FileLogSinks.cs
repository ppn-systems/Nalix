using Notio.Logging.Format;
using Notio.Logging.Interfaces;
using Notio.Logging.Metadata;
using Notio.Logging.Storage;
using System;

namespace Notio.Logging.Sinks;

/// <summary>
/// Trình ghi tệp chung hoạt động theo cách tiêu chuẩn.
/// </summary>
public class FileLogSinks(ILoggerFormatter loggerFormatter, LoggerProvider loggerPrv) : ILoggerSinks
{
    private readonly ILoggerFormatter _loggerFormatter = loggerFormatter;
    private readonly LoggerProvider _loggerPrv = loggerPrv;

    public FileLogSinks(string filename) : this(new LoggerFormatter(), new LoggerProvider(filename)) { }

    public void Publish(LogMessage logMessage)
    {
        if (_loggerPrv.Options.FilterLogEntry != null)
            if (!_loggerPrv.Options.FilterLogEntry(logMessage))
                return;

        if (_loggerPrv.FormatLogEntry != null)
        {
            _loggerPrv.WriteEntry(_loggerPrv.FormatLogEntry(logMessage));
        }
        else
        {
            _loggerPrv.WriteEntry(
                _loggerFormatter.FormatLog(
                    logMessage, _loggerPrv.UseUtcTimestamp ? DateTime.UtcNow : DateTime.Now));
        }
    }
}