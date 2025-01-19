using Notio.Http.Enums;

namespace Notio.Http.Core;

public record HttpResponse(
    HttpStatusCode StatusCode,
    object Data = null,
    string Error = null,
    string Message = null)
{
    public bool Success => StatusCode is >= HttpStatusCode.Ok and < HttpStatusCode.End;

    public static HttpResponse Ok(object data = null, string message = "Request succeeded")
        => new(HttpStatusCode.Ok, data, Message: message);

    public static HttpResponse Created(object data = null, string message = "Resource created")
        => new(HttpStatusCode.Created, data, Message: message);

    public static HttpResponse Fail(string error, string message = "Request failed")
        => new(HttpStatusCode.BadRequest, Error: error, Message: message);

    public static HttpResponse BadRequest(string error, string message = "Bad request")
        => new(HttpStatusCode.BadRequest, Error: error, Message: message);

    public static HttpResponse NotFound(string error, string message = "Resource not found")
        => new(HttpStatusCode.NotFound, Error: error, Message: message);

    public static HttpResponse InternalError(string error, string message = "Internal server error")
        => new(HttpStatusCode.InternalError, Error: error, Message: message);
}