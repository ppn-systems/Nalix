using Notio.Common.Model;
using System.Threading.Tasks;

namespace Notio.Network.Http;

public abstract class HttpController
{
    /// <summary>
    /// Trả về một kết quả thành công với dữ liệu.
    /// </summary>
    /// <param name="data">Dữ liệu phản hồi.</param>
    /// <returns>Kết quả HTTP thành công.</returns>
    protected static Task<HttpResult> Ok(object? data = null)
        => Task.FromResult(HttpResult.Ok(data));

    /// <summary>
    /// Trả về một kết quả thất bại với thông báo lỗi.
    /// </summary>
    /// <param name="message">Thông báo lỗi.</param>
    /// <returns>Kết quả HTTP thất bại.</returns>
    protected static Task<HttpResult> Error(string message)
        => Task.FromResult(HttpResult.Fail(message));
}