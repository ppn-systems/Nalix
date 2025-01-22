using Notio.Logging;
using Notio.Network.Http.Core;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Notio.Network.Http.Middleware;

public class RequestLoggingMiddleware(NotioLog logger) : MiddlewareBase
{
    private readonly NotioLog _logger = logger;
    private readonly Stopwatch _stopwatch = new();

    protected override async Task HandleAsync(HttpContext context)
    {
        _stopwatch.Restart();

        string safeHttpMethod = context.Request.HttpMethod ?? "Unknown";
        string safeUrl = context.Request.Url?.PathAndQuery?.Replace("\n", "").Replace("\r", "") ?? "Unknown URL";

        _logger.Trace($"""
        Completed Request:
        {safeHttpMethod} {safeUrl}
        Status Code: {context.Response.StatusCode}
        Duration: {_stopwatch.ElapsedMilliseconds}ms
        """);

        try
        {
            // Cho phép request tiếp tục pipeline
            await Task.CompletedTask;
        }
        finally
        {
            _stopwatch.Stop();

            // Log response
            _logger.Trace($"""
            Completed Request:
            {safeHttpMethod} {safeUrl}
            Status Code: {context.Response.StatusCode}
            Duration: {_stopwatch.ElapsedMilliseconds}ms
            """);
        }
    }
}