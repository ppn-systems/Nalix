using Notio.Http.Enums;

namespace Notio.Http.Core;

public record HttpResponse(
    HttpStatusCode StatusCode,
    object Data = null,
    string Error = null,
    string Message = null)
{
    public bool Success => StatusCode is >= HttpStatusCode.Ok and < HttpStatusCode.End;
}