using Notio.Logging.Interfaces;
using Notio.Logging.Metadata;
using System;

namespace Notio.Logging.Base;

public abstract class LoggerBase
{
    private readonly LoggerPublisher _logPublisher = new();

    internal LoggerOptions Options { get; private set; } = new();

    /// <summary>
    /// Lấy đối tượng quản lý ghi nhật ký.
    /// </summary>
    public ILoggerPublisher LoggerManager => _logPublisher;

    /// <summary>
    /// Mức ghi log tối thiểu.
    /// </summary>
    public LogLevel MinLevel
    {
        get => Options.MinLevel;
        set { Options.MinLevel = value; }
    }

    public bool IsEnabled(LogLevel logLevel)
        => logLevel >= MinLevel;

    /// <summary>
    /// Ghi một thông điệp với mức độ chỉ định.
    /// </summary>
    protected void PublishLog(
        LogLevel level, EventId eventId, string message, Exception exception = null)
    {
        if (!IsEnabled(level))
            return;

        _logPublisher.Publish(new LogMessage(level, eventId, message, exception));
    }
}