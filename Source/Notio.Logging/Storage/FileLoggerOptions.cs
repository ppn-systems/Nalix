using Notio.Common.Enums;
using System;

namespace Notio.Logging.Storage;

/// <summary>
/// Tùy chọn cấu hình cho bộ ghi log tệp tin.
/// </summary>
public class FileLoggerOptions
{
    /// <summary>
    /// Ghi thêm vào các tệp log hiện có hay ghi đè chúng.
    /// </summary>
    public bool Append { get; set; } = true;

    /// <summary>
    /// Giới hạn kích thước tối đa của một tệp log.
    /// </summary>
    public int MaxFileSize = 10 * 1024 * 1024;

    /// <summary>
    /// Giới hạn kích thước tối đa của một tệp log.
    /// </summary>
    /// <remarks>
    /// Nếu giới hạn kích thước tệp được đặt, logger sẽ tạo tệp mới khi đạt giới hạn.
    /// </remarks>
    public int FileSizeLimitBytes { get; set; } = 3;

    /// <summary>
    /// Mức ghi log tối thiểu cho bộ ghi log tệp tin.
    /// </summary>
    public LoggingLevel MinLevel { get; set; } = LoggingLevel.Trace;

    /// <summary>
    /// Bộ định dạng tùy chỉnh cho tên tệp log.
    /// </summary>
    /// <remarks>
    /// Bằng cách xác định bộ định dạng tùy chỉnh, bạn có thể đặt tiêu chí của riêng mình cho việc tạo tệp log.
    /// Lưu ý rằng bộ định dạng này được gọi mỗi khi ghi thông báo log; bạn nên lưu trữ kết quả để tránh ảnh hưởng hiệu suất.
    /// </remarks>
    /// <example>
    /// fileLoggerOpts.FormatLogFileName = (fname) => {
    ///   return String.Format(Path.GetFileNameWithoutExtension(fname) + "_{0:yyyy}-{0:MM}-{0:dd}" + Path.GetExtension(fname), TimeStamp.UtcNow);
    /// };
    /// </example>
    public Func<string, string>? FormatLogFileName { get; set; }

    /// <summary>
    /// Bộ xử lý tùy chỉnh cho lỗi tệp log.
    /// </summary>
    /// <remarks>
    /// Nếu bộ xử lý này được cung cấp, ngoại lệ mở tệp (khi tạo <code>FileLoggerProvider</code>) sẽ bị loại bỏ.
    /// Bạn có thể xử lý lỗi tệp theo logic của ứng dụng và đề xuất một tên tệp log thay thế (nếu muốn giữ bộ ghi log hoạt động).
    /// </remarks>
    /// <example>
    /// fileLoggerOpts.HandleFileError = (err) => {
    ///   err.UseNewLogFileName(Path.GetFileNameWithoutExtension(err.LogFileName) + "_alt" + Path.GetExtension(err.LogFileName));
    /// };
    /// </example>
    public Action<FileError>? HandleFileError { get; set; }
}