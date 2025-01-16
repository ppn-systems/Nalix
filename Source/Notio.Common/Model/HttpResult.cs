using System.Collections.Generic;

namespace Notio.Common.Model;

public record HttpResult(
    int StatusCode,
    object Data = null,
    string Error = null,
    string Message = null,
    Dictionary<string, object> Metadata = null)
{
    public bool Success => StatusCode is >= 200 and < 300;

    public static HttpResult Ok(object data = null, string message = "Request succeeded")
        => new(200, data, Message: message);

    public static HttpResult Created(object data = null, string message = "Resource created")
        => new(201, data, Message: message);

    public static HttpResult Fail(string error, string message = "Request failed")
        => new(400, Error: error, Message: message); 

    public static HttpResult BadRequest(string error, string message = "Bad request")
        => new(400, Error: error, Message: message);

    public static HttpResult NotFound(string error, string message = "Resource not found")
        => new(404, Error: error, Message: message);

    public static HttpResult InternalError(string error, string message = "Internal server error")
        => new(500, Error: error, Message: message);
}