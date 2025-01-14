using Notio.Network.Https.Attributes;
using Notio.Network.Https.Model;
using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Tasks;

namespace Notio.Network.Https;

public class NotioRouter
{
    private readonly ConcurrentDictionary<string, Func<NotioHttpsContext, Task<ApiResponse>>> _routes = new();

    public void RegisterController<T>() where T : NotioHttpsController, new()
    {
        var type = typeof(T);
        if (!type.IsDefined(typeof(ApiControllerAttribute), false))
            return;

        var controller = new T();
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);

        foreach (var method in methods)
        {
            var attribute = method.GetCustomAttribute<HttpsRouteAttribute>();
            if (attribute == null) continue;

            string routeKey = $"{attribute.Method}:{attribute.Path}";
            _routes.TryAdd(routeKey, async context =>
            {
                return await (Task<ApiResponse>)method.Invoke(controller, [context])!;
            });
        }
    }

    public async Task<ApiResponse> RouteAsync(NotioHttpsContext context)
    {
        string routeKey = $"{context.Request.HttpMethod}:{context.Request.Url?.AbsolutePath}";

        if (_routes.TryGetValue(routeKey, out var handler))
        {
            return await handler(context);
        }

        return new ApiResponse { Error = "Route not found" };
    }
}