using Notio.Common.Models;
using System;

namespace Notio.Common.Logging;

/// <summary>
/// Giao diện định nghĩa trình định dạng cho các thông điệp nhật ký.
/// </summary>
public interface ILoggingFormatter
{
    /// <summary>
    /// Định dạng một thông điệp nhật ký dựa trên thông tin đã cung cấp.
    /// </summary>
    /// <param name="logMsg">Thông điệp nhật ký cần định dạng.</param>
    /// <returns>Một chuỗi đã được định dạng đại diện cho thông điệp nhật ký.</returns>
    string FormatLog(LoggingEntry logMsg);
}