using Notio.Common.Logging;
using Notio.Logging.Enums;
using Notio.Logging.Interfaces;
using Notio.Logging.Metadata;
using Notio.Logging.Storage;
using System;

namespace Notio.Logging.Engine;

public abstract class LoggingEngine
{
    private readonly LoggingPublisher _publisher = new();
    internal FileLoggerOptions Options { get; init; } = new();

    /// <summary>
    /// Lấy đối tượng quản lý ghi nhật ký.
    /// </summary>
    public ILoggingPublisher Publisher => _publisher;

    /// <summary>
    /// Mức ghi log tối thiểu.
    /// </summary>
    public LoggingLevel MinimumLevel = LoggingLevel.Trace;

    protected bool CanLog(LoggingLevel level) => level >= MinimumLevel;

    /// <summary>
    /// Ghi một thông điệp với mức độ chỉ định.
    /// </summary>
    protected void CreateLogEntry(LoggingLevel level, EventId eventId, string message, Exception? error = null)
    {
        if (!CanLog(level)) return;
        _publisher.Publish(new LoggingEntry(level, eventId, message, error));
    }
}