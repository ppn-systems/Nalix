using Notio.Logging.Base;
using Notio.Logging.Format;
using Notio.Logging.Metadata;
using Notio.Logging.Sinks;
using System;
using System.Reflection.Emit;

namespace Notio.Logging;

/// <summary>
/// Lớp NotioLog cung cấp các phương thức ghi log cho các sự kiện.
/// </summary>
public sealed class NotioLog : LoggerBase
{
    private static readonly Lazy<NotioLog> _instance = new(() => new NotioLog());

    private NotioLog() { }

    /// <summary>
    /// Lấy thể hiện duy nhất của NotioLog.
    /// </summary>
    public static NotioLog Instance => _instance.Value;

    /// <summary>
    /// Khởi tạo mặc định các handler.
    /// </summary>
    public void DefaultInitialization()
    {
        base.LoggerManager
            .AddHandler(new ConsoleLogSinks())
            .AddHandler(new FileLogSinks("Notio"));
    }

    /// <summary>
    /// Ghi log một thông báo với mức độ và ID sự kiện.
    /// </summary>
    /// <param name="level">Mức độ log.</param>
    /// <param name="eventId">ID sự kiện.</param>
    /// <param name="message">Thông báo log.</param>
    public void Log(LogLevel level, EventId eventId, string message)
        => PublishLog(level, eventId, message);

    /// <summary>
    /// Ghi log một ngoại lệ với mức độ và ID sự kiện.
    /// </summary>
    /// <param name="level">Mức độ log.</param>
    /// <param name="eventId">ID sự kiện.</param>
    /// <param name="exception">Ngoại lệ.</param>
    public void Log(LogLevel level, EventId eventId, Exception exception)
        => PublishLog(level, eventId, exception.Message, exception);

    /// <summary>
    /// Ghi log một thông báo thông tin.
    /// </summary>
    /// <param name="eventId">ID sự kiện.</param>
    /// <param name="message">Thông báo log.</param>
    public void Info(EventId eventId, string message)
        => Log(LogLevel.Information, eventId, message);

    /// <summary>
    /// Ghi log một thông báo gỡ lỗi.
    /// </summary>
    /// <param name="eventId">ID sự kiện.</param>
    /// <param name="message">Thông báo log.</param>
    public void Debug(EventId eventId, string message)
        => Log(LogLevel.Debug, eventId, message);

    /// <summary>
    /// Ghi log một thông báo theo dõi.
    /// </summary>
    /// <param name="eventId">ID sự kiện.</param>
    /// <param name="message">Thông báo log.</param>
    public void Trace(EventId eventId, string message)
        => Log(LogLevel.Trace, eventId, message);

    /// <summary>
    /// Ghi log một thông báo cảnh báo.
    /// </summary>
    /// <param name="eventId">ID sự kiện.</param>
    /// <param name="message">Thông báo log.</param>
    public void Warning(EventId eventId, string message)
        => Log(LogLevel.Warning, eventId, message);

    /// <summary>
    /// Ghi log một ngoại lệ với mức độ lỗi.
    /// </summary>
    /// <param name="eventId">ID sự kiện.</param>
    /// <param name="exception">Ngoại lệ.</param>
    public void Error(EventId eventId, Exception exception)
        => Log(LogLevel.Error, eventId, exception);

    /// <summary>
    /// Ghi log một ngoại lệ với mức độ lỗi và thông báo tùy chỉnh.
    /// </summary>
    /// <param name="eventId">ID sự kiện.</param>
    /// <param name="message">Thông báo log.</param>
    /// <param name="exception">Ngoại lệ.</param>
    public void Error(EventId eventId, string message, Exception exception)
        => PublishLog(LogLevel.Error, eventId, message, exception);
}