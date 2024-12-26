namespace Notio.Logging.Interfaces;

/// <summary>
/// Định nghĩa giao diện cho nhà xuất bản nhật ký.
/// </summary>
public interface ILogingPublisher
{
    /// <summary>
    /// Thêm một đối tượng xử lý nhật ký.
    /// </summary>
    /// <param name="loggerHandler">Đối tượng xử lý nhật ký.</param>
    /// <returns>Đối tượng <see cref="ILoggingSinks"/> hiện tại.</returns>
    ILogingPublisher AddHandler(ILoggingSinks loggerHandler);

    /// <summary>
    /// Xóa một đối tượng xử lý nhật ký.
    /// </summary>
    /// <param name="loggerHandler">Đối tượng xử lý nhật ký cần xóa.</param>
    /// <returns><c>true</c> nếu đối tượng xử lý đã được xóa thành công, <c>false</c> nếu không.</returns>
    bool RemoveHandler(ILoggingSinks loggerHandler);
}