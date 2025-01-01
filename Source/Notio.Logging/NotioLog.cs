using Notio.Logging.Metadata;
using Notio.Logging.Engine;
using Notio.Logging.Targets;
using System;
using System.Runtime.CompilerServices;
using System.IO.Enumeration;

namespace Notio.Logging;

/// <summary>
/// Lớp NotioLog cung cấp các phương thức ghi log cho các sự kiện.
/// </summary>
public sealed class NotioLog : LoggingEngine
{
    private bool _isInitialized;
    public static readonly EventId Empty = new(0);
    private static readonly Lazy<NotioLog> _instance = new(() => new());

    /// <summary>
    /// Khởi tạo một instance mới của lớp NotioLog.
    /// </summary>
    private NotioLog()
    { }

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
        if (_isInitialized) throw new InvalidOperationException("Logging has already been initialized.");
        _isInitialized = true;

        LoggingBuilder builder = new(base.Publisher);
        configure?.Invoke(builder);

        if (builder.IsDefaults)
        {
            base.Publisher
                .AddTarget(new ConsoleTarget())
                .AddTarget(new FileTarget(builder.LogDirectory, builder.LogFileName));
        }
    }

    public void ConfigureDefaults(Func<LoggingBuilder, LoggingBuilder> defaults)
    {
        LoggingBuilder builder = new(base.Publisher);
        defaults(builder);
    }

    /// <summary>
    /// Ghi log với mức độ, ID sự kiện và thông điệp.
    /// </summary>
    /// <param name="level">Mức độ log.</param>
    /// <param name="eventId">ID sự kiện.</param>
    /// <param name="message">Thông điệp log.</param>
    public void Write(LoggingLevel level, EventId eventId, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        base.CreateLogEntry(level, eventId, message);
    }

    /// <summary>
    /// Ghi log với mức độ, ID sự kiện và ngoại lệ.
    /// </summary>
    /// <param name="level">Mức độ log.</param>
    /// <param name="eventId">ID sự kiện.</param>
    /// <param name="exception">Ngoại lệ.</param>
    public void Write(LoggingLevel level, EventId eventId, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        base.CreateLogEntry(level, eventId, $"{exception.Message}\n{exception.StackTrace}", exception);
    }

    /// <summary>
    /// Ghi log với mức độ, ID sự kiện và ngoại lệ.
    /// </summary>
    /// <param name="level">Mức độ log.</param>
    /// <param name="eventId">ID sự kiện.</param>
    /// <param name="exception">Ngoại lệ.</param>
    public void Write(LoggingLevel level, EventId eventId, string message, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        base.CreateLogEntry(level, eventId, message, exception);
    }

    /// <summary>
    /// Ghi log thông tin với định dạng chuỗi.
    /// </summary>
    /// <param name="format">Chuỗi định dạng.</param>
    /// <param name="args">Các tham số để định dạng chuỗi.</param>
    public void Info(string format, params object[] args)
        => Write(LoggingLevel.Information, Empty, string.Format(format, args));

    /// <summary>
    /// Ghi log thông tin.
    /// </summary>
    /// <param name="message">Thông điệp log.</param>
    /// <param name="eventId">ID sự kiện (tùy chọn).</param>
    public void Info(string message, EventId? eventId = null)
        => Write(LoggingLevel.Information, eventId ?? Empty, message);

    /// <summary>
    /// Ghi log debug.
    /// </summary>
    /// <param name="message">Thông điệp log.</param>
    /// <param name="eventId">ID sự kiện (tùy chọn).</param>
    public void Debug(string message, EventId? eventId = null, [CallerMemberName] string memberName = "")
        => Write(LoggingLevel.Debug, eventId ?? Empty, $"[{memberName}] {message}");

    /// <summary>
    /// Ghi log trace.
    /// </summary>
    /// <param name="message">Thông điệp log.</param>
    /// <param name="eventId">ID sự kiện (tùy chọn).</param>
    public void Trace(string message, EventId? eventId = null)
        => Write(LoggingLevel.Trace, eventId ?? Empty, message);

    /// <summary>
    /// Ghi log cảnh báo.
    /// </summary>
    /// <param name="message">Thông điệp log.</param>
    /// <param name="eventId">ID sự kiện (tùy chọn).</param>
    public void Warn(string message, EventId? eventId = null)
        => Write(LoggingLevel.Warning, eventId ?? Empty, message);

    /// <summary>
    /// Ghi log lỗi với ngoại lệ.
    /// </summary>
    /// <param name="exception">Ngoại lệ.</param>
    /// <param name="eventId">ID sự kiện (tùy chọn).</param>
    public void Error(Exception exception, EventId? eventId = null)
        => Write(LoggingLevel.Error, eventId ?? Empty, exception);

    /// <summary>
    /// Ghi log lỗi với thông điệp và ngoại lệ.
    /// </summary>
    /// <param name="message">Thông điệp log.</param>
    /// <param name="exception">Ngoại lệ.</param>
    /// <param name="eventId">ID sự kiện (tùy chọn).</param>
    public void Error(string message, Exception exception, EventId? eventId = null)
        => Write(LoggingLevel.Error, eventId ?? Empty, message, exception);

    /// <summary>
    /// Ghi log lỗi nghiêm trọng.
    /// </summary>
    /// <param name="message">Thông điệp log.</param>
    /// <param name="eventId">ID sự kiện (tùy chọn).</param>
    public void Fatal(string message, EventId? eventId = null)
        => Write(LoggingLevel.Critical, eventId ?? Empty, message);

    /// <summary>
    /// Ghi log lỗi nghiêm trọng.
    /// </summary>
    /// <param name="message">Thông điệp log.</param>
    /// <param name="exception">Ngoại lệ (tùy chọn).</param>
    /// <param name="eventId">ID sự kiện (tùy chọn).</param>
    public void Fatal(string message, Exception exception, EventId? eventId = null)
        => Write(LoggingLevel.Critical, eventId ?? Empty, message, exception);
}