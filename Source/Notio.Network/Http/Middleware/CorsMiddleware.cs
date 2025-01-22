using Notio.Network.Http.Core;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Notio.Network.Http.Middleware;

public sealed class CorsMiddleware(HttpConfig? httpConfig = null) : MiddlewareBase
{
    private readonly string[] _allowedOrigins = httpConfig?.AllowedOrigins ?? ["*"];
    private readonly string[] _allowedMethods = httpConfig?.AllowedMethods ?? ["GET", "POST", "PUT", "DELETE", "OPTIONS"];
    private readonly string[] _allowedHeaders = httpConfig?.AllowedHeaders ?? ["Content-Type", "Authorization"];

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
            response.StatusCode = (int)System.Net.HttpStatusCode.NoContent;
            return Task.CompletedTask;
        }

        return Task.CompletedTask;
    }
}