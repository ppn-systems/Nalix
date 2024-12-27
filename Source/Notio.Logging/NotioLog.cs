using Notio.Logging.Metadata;
using Notio.Logging.Targets;
using System;

namespace Notio.Logging;

/// <summary>
/// Lớp NotioLog cung cấp các phương thức ghi log cho các sự kiện.
/// </summary>
public sealed class NotioLog : LoggingEngine
{
    private static readonly Lazy<NotioLog> _instance = new(() => new());
    public static readonly EventId Empty = new(0);

    /// <summary>
    /// Khởi tạo một instance mới của lớp NotioLog.
    /// </summary>
    private NotioLog() { }

    /// <summary>
    /// Lấy instance duy nhất của lớp NotioLog.
    /// </summary>
    public static NotioLog Instance => _instance.Value;

    /// <summary>
    /// Khởi tạo hệ thống logging với cấu hình tùy chọn.
    /// </summary>
    /// <param name="configure">Hành động để cấu hình <see cref="LoggingBuilder"/>.</param>
    public void Initialize(Action<LoggingBuilder>? configure = null)
    {
        LoggingBuilder builder = new(Publisher);
        configure?.Invoke(builder);

        if (builder.UseDefaults)
        {
            Publisher
                .AddTarget(new ConsoleTarget())
                .AddTarget(new FileTarget());
        }
    }

    /// <summary>
    /// Ghi log với mức độ, ID sự kiện và thông điệp.
    /// </summary>
    /// <param name="level">Mức độ log.</param>
    /// <param name="eventId">ID sự kiện.</param>
    /// <param name="message">Thông điệp log.</param>
    public void Write(LogLevel level, EventId eventId, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        base.CreateLogEntry(level, eventId, message);
    }

    /// <summary>
    /// Ghi log với mức độ, ID sự kiện và ngoại lệ.
    /// </summary>
    /// <param name="level">Mức độ log.</param>
    /// <param name="eventId">ID sự kiện.</param>
    /// <param name="ex">Ngoại lệ.</param>
    public void Write(LogLevel level, EventId eventId, Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);
        base.CreateLogEntry(level, eventId, ex.Message, ex);
    }

    /// <summary>
    /// Ghi log thông tin.
    /// </summary>
    /// <param name="message">Thông điệp log.</param>
    /// <param name="eventId">ID sự kiện (tùy chọn).</param>
    public void Info(string message, EventId? eventId = null)
        => Write(LogLevel.Information, eventId ?? Empty, message);

    /// <summary>
    /// Ghi log debug.
    /// </summary>
    /// <param name="message">Thông điệp log.</param>
    /// <param name="eventId">ID sự kiện (tùy chọn).</param>
    public void Debug(string message, EventId? eventId = null)
        => Write(LogLevel.Debug, eventId ?? Empty, message);

    /// <summary>
    /// Ghi log trace.
    /// </summary>
    /// <param name="message">Thông điệp log.</param>
    /// <param name="eventId">ID sự kiện (tùy chọn).</param>
    public void Trace(string message, EventId? eventId = null)
        => Write(LogLevel.Trace, eventId ?? Empty, message);

    /// <summary>
    /// Ghi log cảnh báo.
    /// </summary>
    /// <param name="message">Thông điệp log.</param>
    /// <param name="eventId">ID sự kiện (tùy chọn).</param>
    public void Warn(string message, EventId? eventId = null)
        => Write(LogLevel.Warning, eventId ?? Empty, message);

    /// <summary>
    /// Ghi log lỗi với ngoại lệ.
    /// </summary>
    /// <param name="ex">Ngoại lệ.</param>
    /// <param name="eventId">ID sự kiện (tùy chọn).</param>
    public void Error(Exception ex, EventId? eventId = null)
        => Write(LogLevel.Error, eventId ?? Empty, ex);

    /// <summary>
    /// Ghi log lỗi với thông điệp và ngoại lệ.
    /// </summary>
    /// <param name="message">Thông điệp log.</param>
    /// <param name="ex">Ngoại lệ.</param>
    /// <param name="eventId">ID sự kiện (tùy chọn).</param>
    public void Error(string message, Exception ex, EventId? eventId = null)
    {
        ArgumentNullException.ThrowIfNull(ex);
        base.CreateLogEntry(LogLevel.Error, eventId ?? Empty, message, ex);
    }
}
