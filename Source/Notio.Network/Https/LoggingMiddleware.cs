using Notio.Logging;
using Notio.Network.Https.Model;
using System;
using System.Threading.Tasks;

namespace Notio.Network.Https
{
    public class LoggingMiddleware : IMiddleware
    {
        public async Task InvokeAsync(NotioHttpsContext context)
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
