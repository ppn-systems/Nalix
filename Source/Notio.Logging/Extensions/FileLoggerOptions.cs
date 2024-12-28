using Notio.Logging.Metadata;
using System;

namespace Notio.Logging.Extensions;

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
    /// <remarks>
    /// Nếu giới hạn kích thước tệp được đặt, logger sẽ tạo tệp mới khi đạt giới hạn.
    /// Ví dụ: nếu tên tệp log là 'test.log', logger sẽ tạo 'test1.log', 'test2.log', v.v.
    /// </remarks>
    public long FileSizeLimitBytes { get; set; } = 0;

    /// <summary>
    /// Giới hạn số lượng tệp log nếu <see cref="FileSizeLimitBytes"/> được đặt.
    /// </summary>
    /// <remarks>
    /// Nếu MaxRollingFiles được đặt, logger sẽ ghi đè các tệp log đã tạo trước đó.
    /// Ví dụ: nếu tên tệp log là 'test.log' và số lượng tệp tối đa là 3, logger sẽ sử dụng: 'test.log', 'test1.log', 'test2.log' và sau đó lại 'test.log' (nội dung cũ bị xóa).
    /// </remarks>
    public int MaxRollingFiles { get; set; } = 0;

    /// <summary>
    /// Mức ghi log tối thiểu cho bộ ghi log tệp tin.
    /// </summary>
    public LogLevel MinLevel { get; set; } = LogLevel.Trace;

    /// <summary>
    /// Bộ định dạng tùy chỉnh cho tên tệp log.
    /// </summary>
    /// <remarks>
    /// Bằng cách xác định bộ định dạng tùy chỉnh, bạn có thể đặt tiêu chí của riêng mình cho việc tạo tệp log.
    /// Lưu ý rằng bộ định dạng này được gọi mỗi khi ghi thông báo log; bạn nên lưu trữ kết quả để tránh ảnh hưởng hiệu suất.
    /// </remarks>
    /// <example>
    /// fileLoggerOpts.FormatLogFileName = (fname) => {
    ///   return String.Format(Path.GetFileNameWithoutExtension(fname) + "_{0:yyyy}-{0:MM}-{0:dd}" + Path.GetExtension(fname), DateTime.UtcNow);
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

    /// <summary>
    /// Định nghĩa quy ước đặt tên và thứ tự của các tệp log quay vòng.
    /// </summary>
    public FileRollingConvention RollingFilesConvention { get; set; } = FileRollingConvention.Ascending;

    /// <summary>
    /// Quy ước quay vòng tệp log khác nhau, mặc định là Ascending.
    /// </summary>
    public enum FileRollingConvention
    {
        /// <summary>
        /// (Mặc định) Tệp mới sẽ nhận chỉ số quay vòng tăng dần, tệp được quay vòng sau tối đa 0-1-2-3-0-1-2-3.
        /// </summary>
        Ascending,

        /// <summary>
        /// Tệp mới sẽ nhận chỉ số quay vòng tăng dần, nhưng tệp mới nhất luôn là tệp không có chỉ số. Tùy chọn thay thế hiệu quả hơn cho quay vòng giảm dần. 0-1-2-3-1-2-3
        /// </summary>
        AscendingStableBase,

        /// <summary>
        /// Ghi log kiểu Unix giảm dần, tệp cơ sở luôn ổn định và chứa nhật ký mới nhất, tệp mới sẽ được tăng và đổi tên để số cao nhất luôn là tệp cũ nhất. 0-1-2-3
        /// </summary>
        Descending
    }
}