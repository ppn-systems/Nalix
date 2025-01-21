using System.Net;

namespace Notio.Http.Core;

public record HttpResponse(
    HttpStatusCode StatusCode,
    object Data = default,
    string Error = null,
    string Message = null)
{
    // Tự động tính trạng thái thành công
    public bool Success => StatusCode >= HttpStatusCode.OK && StatusCode < HttpStatusCode.MultipleChoices;
}