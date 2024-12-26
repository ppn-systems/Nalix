namespace Notio.Logging.Metadata;

/// <summary>
/// Đại diện một thông điệp nhật ký trong hệ thống logging.
/// </summary>
public readonly struct LogMessage
{
    /// <summary>
    /// Nội dung của thông điệp nhật ký.
    /// </summary>
    public readonly string Message;

    /// <summary>
    /// Mức độ nhật ký của thông điệp.
    /// </summary>
    public readonly LogLevel LogLevel;

    /// <summary>
    /// ID sự kiện liên quan đến thông điệp nhật ký.
    /// </summary>
    public readonly EventId EventId;

    /// <summary>
    /// Ngoại lệ đi kèm thông điệp, nếu có.
    /// </summary>
    public readonly System.Exception? Exception;

    /// <summary>
    /// Khởi tạo một thông điệp nhật ký mới.
    /// </summary>
    /// <param name="level">Mức độ nhật ký của thông điệp.</param>
    /// <param name="eventId">ID sự kiện liên quan đến thông điệp.</param>
    /// <param name="message">Nội dung của thông điệp nhật ký.</param>
    /// <param name="exception">Ngoại lệ đi kèm (nếu có).</param>
    internal LogMessage(LogLevel level, EventId eventId, string message, System.Exception? exception = null)
    {
        Message = message;
        LogLevel = level;
        EventId = eventId;
        Exception = exception;
    }
}
