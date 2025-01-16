using Notio.Common.Http;
using Notio.Common.Model;
using Notio.Logging;
using System;
using System.Threading.Tasks;

namespace Notio.Network.Http
{
    public class LoggingMiddleware : IMiddleware
    {
        public async Task InvokeAsync(HttpContext context)
        {
            var startTime = DateTime.UtcNow;

            NotioLog.Instance.Info($"Request: {context.Request.HttpMethod} {context.Request.Url?.PathAndQuery}");

            // Continue processing
            await Task.CompletedTask;

            var duration = DateTime.UtcNow - startTime;
            NotioLog.Instance.Info($"Response: {context.Response.StatusCode} Duration: {duration.TotalMilliseconds}ms");
        }
    }
}
