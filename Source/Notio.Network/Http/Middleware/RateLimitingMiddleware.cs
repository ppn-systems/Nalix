using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Notio.Network.Http.Middleware;

public sealed class RateLimitingMiddleware(HttpConfig? httpConfig = null)
    : MiddlewareBase
{
    private readonly int _maxRequests = httpConfig?.MaxRequests ?? 100;
    private readonly TimeSpan _window = TimeSpan.FromMinutes(httpConfig?.WindowMinutes ?? 15);
    private readonly Dictionary<string, (int Count, DateTime Window)> _requests = [];

    protected override Task HandleAsync(HttpContext context)
    {
        string ip = context.Request.RemoteEndPoint.Address.ToString();
        DateTime now = DateTime.UtcNow;

        lock (_requests)
        {
            if (_requests.TryGetValue(ip, out var data))
            {
                if (now - data.Window > _window)
                    _requests[ip] = (1, now);
                else if (data.Count >= _maxRequests)
                {
                    context.Response.StatusCode = (int)System.Net.HttpStatusCode.TooManyRequests;
                    throw new HttpRequestException("Rate limit exceeded");
                }
                else
                    _requests[ip] = (data.Count + 1, data.Window);
            }
            else
                _requests[ip] = (1, now);
        }

        if (Random.Shared.Next(100) < 10) CleanupOldEntries();

        return Task.CompletedTask;
    }

    private void CleanupOldEntries()
    {
        DateTime now = DateTime.UtcNow;
        lock (_requests)
        {
            var oldEntries = _requests.Where(kvp => now - kvp.Value.Window > _window).Select(kvp => kvp.Key).ToList();
            foreach (var key in oldEntries) _requests.Remove(key);
        }
    }
}