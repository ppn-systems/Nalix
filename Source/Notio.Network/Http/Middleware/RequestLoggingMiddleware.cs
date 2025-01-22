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

        // Log request
        _logger.Trace($"""
        Incoming Request:
        {context.Request.HttpMethod} {context.Request.Url?.PathAndQuery}
        Remote IP: {context.Request.RemoteEndPoint.Address}
        Headers: {string.Join(", ", context.Request.Headers.AllKeys.Select(k => $"{k}: {context.Request.Headers[k]}"))}
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
            {context.Request.HttpMethod} {context.Request.Url?.PathAndQuery}
            Status Code: {context.Response.StatusCode}
            Duration: {_stopwatch.ElapsedMilliseconds}ms
            """);
        }
    }
}