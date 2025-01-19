using System;

namespace Notio.Common.Exceptions;

/// <summary>
/// Ngoại lệ tùy chỉnh cho các lỗi liên quan đến packet.
/// </summary>
public class PacketException : Exception
{
    /// <summary>
    /// Mã lỗi tùy chọn để định danh lỗi.
    /// </summary>
    public int ErrorCode { get; }

    /// <summary>
    /// Khởi tạo một ngoại lệ packet với thông báo lỗi.
    /// </summary>
    /// <param name="message">Thông báo lỗi.</param>
    public PacketException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Khởi tạo một ngoại lệ packet với thông báo và mã lỗi.
    /// </summary>
    /// <param name="message">Thông báo lỗi.</param>
    /// <param name="errorCode">Mã lỗi.</param>
    public PacketException(string message, int errorCode)
        : base(message) => ErrorCode = errorCode;

    /// <summary>
    /// Khởi tạo một ngoại lệ packet với thông báo và ngoại lệ gốc.
    /// </summary>
    /// <param name="message">Thông báo lỗi.</param>
    /// <param name="innerException">Ngoại lệ gốc.</param>
    public PacketException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Khởi tạo một ngoại lệ packet với thông báo, mã lỗi và ngoại lệ gốc.
    /// </summary>
    /// <param name="message">Thông báo lỗi.</param>
    /// <param name="errorCode">Mã lỗi.</param>
    /// <param name="innerException">Ngoại lệ gốc.</param>
    public PacketException(string message, int errorCode, Exception innerException)
        : base(message, innerException) => ErrorCode = errorCode;
}