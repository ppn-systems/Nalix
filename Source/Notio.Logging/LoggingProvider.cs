using Notio.Logging.Extensions;
using Notio.Logging.Interfaces;
using Notio.Logging.Metadata;
using System;

namespace Notio.Logging;

public abstract class LoggingProvider
{
    private readonly LoggingPublisher _logPublisher = new();

    internal FileLoggerOptions Options { get; private set; } = new();

    /// <summary>
    /// Lấy đối tượng quản lý ghi nhật ký.
    /// </summary>
    public ILogingPublisher LoggerManager => _logPublisher;

    /// <summary>
    /// Mức ghi log tối thiểu.
    /// </summary>
    public LogLevel MinLevel = LogLevel.Trace;

    public bool IsEnabled(LogLevel logLevel)
        => logLevel >= MinLevel;

    /// <summary>
    /// Ghi một thông điệp với mức độ chỉ định.
    /// </summary>
    protected void PublishLog(
        LogLevel level, EventId eventId, string message, Exception? exception = null)
    {
        if (!IsEnabled(level))
            return;

        _logPublisher.Publish(new LogMessage(level, eventId, message, exception));
    }
}