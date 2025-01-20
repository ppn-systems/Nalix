using Notio.Http.Core;
using Notio.Http.Enums;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Notio.Http.Middleware;

public sealed class CorsMiddleware(
    string[] allowedOrigins = null, 
    string[] allowedMethods = null, 
    string[] allowedHeaders = null) : MiddlewareBase
{
    private readonly string[] _allowedOrigins = allowedOrigins ?? ["*"];
    private readonly string[] _allowedMethods = allowedMethods ?? ["GET", "POST", "PUT", "DELETE", "OPTIONS"];
    private readonly string[] _allowedHeaders = allowedHeaders ?? ["Content-Type", "Authorization"];

    protected override Task HandleAsync(HttpContext context)
    {
        var response = context.Response;
        var origin = context.Request.Headers["Origin"];

        if (!string.IsNullOrEmpty(origin) && (_allowedOrigins.Contains("*") || _allowedOrigins.Contains(origin)))
        {
            response.Headers.Add("Access-Control-Allow-Origin", origin);
            response.Headers.Add("Access-Control-Allow-Methods", string.Join(", ", _allowedMethods));
            response.Headers.Add("Access-Control-Allow-Headers", string.Join(", ", _allowedHeaders));
            response.Headers.Add("Access-Control-Allow-Credentials", "true");
        }

        if (context.Request.HttpMethod == "OPTIONS")
        {
            response.StatusCode = (int)HttpStatusCode.NoContent;
            return Task.CompletedTask;
        }

        return Task.CompletedTask;
    }
}
