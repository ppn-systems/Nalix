using Notio.Database.Model;
using Notio.Http.Enums;
using System.Threading.Tasks;

namespace Notio.Http.Core;

public abstract class HttpController
{
    /// <summary>
    /// Trả về một kết quả thành công với dữ liệu.
    /// </summary>
    /// <param name="data">Dữ liệu phản hồi.</param>
    /// <returns>Kết quả HTTP thành công.</returns>
    protected static Task<HttpResponse> Ok(object data = null)
    {
        return Task.FromResult(new HttpResponse(
            HttpStatusCode.Ok,   // StatusCode
            data,                // Data (null nếu không có dữ liệu trả về)
            null,                // Error (chứa thông báo lỗi)
            null                 // Message (có thể để null nếu không có thông báo chi tiết)
    ));
    }

    /// <summary>
    /// Trả về một kết quả thất bại với thông báo lỗi.
    /// </summary>
    /// <param name="message">Thông báo lỗi.</param>
    /// <returns>Kết quả HTTP thất bại.</returns>
    protected static Task<HttpResponse> Fail(string message)
    {
        return Task.FromResult(new HttpResponse(
            HttpStatusCode.BadRequest,   // StatusCode
            null,                        // Data (null nếu không có dữ liệu trả về)
            message,                     // Error (chứa thông báo lỗi)
            null                         // Message (có thể để null nếu không có thông báo chi tiết)
    ));
    }
}
