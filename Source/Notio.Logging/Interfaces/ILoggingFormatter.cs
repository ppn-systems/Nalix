using Notio.Logging.Metadata;
using System;

namespace Notio.Logging.Interfaces;

/// <summary>
/// Giao diện định nghĩa trình định dạng cho các thông điệp nhật ký.
/// </summary>
public interface ILoggingFormatter
{
    /// <summary>
    /// Định dạng một thông điệp nhật ký dựa trên thông tin đã cung cấp.
    /// </summary>
    /// <param name="logMsg">Thông điệp nhật ký cần định dạng.</param>
    /// <param name="timeStamp">Dấu thời gian của thông điệp nhật ký.</param>
    /// <returns>Một chuỗi đã được định dạng đại diện cho thông điệp nhật ký.</returns>
    string FormatLog(LogEntry logMsg, DateTime timeStamp);
}