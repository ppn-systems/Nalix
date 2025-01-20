using Notio.Http.Core;
using Notio.Http.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Notio.Http.Middleware;

public sealed class RateLimitingMiddleware(int maxRequests = 100, int windowMinutes = 15) : MiddlewareBase, IMiddleware
{
    private readonly Dictionary<string, (int Count, DateTime Window)> _requests = [];
    private readonly int _maxRequests = maxRequests;
    private readonly TimeSpan _window = TimeSpan.FromMinutes(windowMinutes);

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
                    context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
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
