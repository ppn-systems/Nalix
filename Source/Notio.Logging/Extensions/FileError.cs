using System;

namespace Notio.Logging.Extensions;

/// <summary>
/// Đại diện cho ngữ cảnh lỗi tệp tin.
/// </summary>
public class FileError
{
    /// <summary>
    /// Ngoại lệ xảy ra trong thao tác tệp tin.
    /// </summary>
    public Exception ErrorException { get; private set; }

    /// <summary>
    /// Tên tệp log hiện tại.
    /// </summary>
    public string LogFileName { get; private set; }

    internal FileError(string logFileName, Exception ex)
    {
        LogFileName = logFileName;
        ErrorException = ex;
    }

    internal string NewLogFileName { get; private set; }

    /// <summary>
    /// Đề xuất một tên tệp log mới để sử dụng thay cho tên hiện tại.
    /// </summary>
    /// <param name="newLogFileName">Tên tệp log mới</param>
    public void UseNewLogFileName(string newLogFileName)
    {
        NewLogFileName = newLogFileName;
    }
}