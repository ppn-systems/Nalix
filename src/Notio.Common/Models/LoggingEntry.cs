using Notio.Common.Logging;
using System;

namespace Notio.Common.Models;

/// <summary>
/// Đại diện một thông điệp nhật ký trong hệ thống logging.
/// </summary>
/// <param name="level">Mức độ nhật ký của thông điệp.</param>
/// <param name="eventId">ID sự kiện liên quan đến thông điệp.</param>
/// <param name="message">Nội dung của thông điệp nhật ký.</param>
/// <param name="exception">Ngoại lệ đi kèm (nếu có).</param>
public readonly struct LoggingEntry(LoggingLevel level, EventId eventId, string message, System.Exception exception = null)
{
    /// <summary>
    /// Thời gian của nhật ký.
    /// </summary>
    public readonly DateTime TimeStamp = DateTime.UtcNow;

    /// <summary>
    /// Nội dung của thông điệp nhật ký.
    /// </summary>
    public readonly string Message = message;

    /// <summary>
    /// Mức độ nhật ký của thông điệp.
    /// </summary>
    public readonly LoggingLevel LogLevel = level;

    /// <summary>
    /// ID sự kiện liên quan đến thông điệp nhật ký.
    /// </summary>
    public readonly EventId EventId = eventId;

    /// <summary>
    /// Ngoại lệ đi kèm thông điệp, nếu có.
    /// </summary>
    public readonly System.Exception Exception = exception;
}