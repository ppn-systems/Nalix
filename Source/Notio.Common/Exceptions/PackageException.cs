using System;

namespace Notio.Common.Exceptions;

/// <summary>
/// Ngoại lệ tùy chỉnh cho các lỗi liên quan đến packet.
/// </summary>
public class PackageException : Exception
{
    /// <summary>
    /// Khởi tạo một ngoại lệ packet với thông báo lỗi.
    /// </summary>
    /// <param name="message">Thông báo lỗi.</param>
    public PackageException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Khởi tạo một ngoại lệ packet với thông báo và ngoại lệ gốc.
    /// </summary>
    /// <param name="message">Thông báo lỗi.</param>
    /// <param name="innerException">Ngoại lệ gốc.</param>
    public PackageException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}