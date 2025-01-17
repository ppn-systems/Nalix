using Notio.Common.Model;
using Notio.Http.Attributes;
using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Tasks;

namespace Notio.Http;

/// <summary>
/// Handles routing of HTTP requests to registered controllers and their methods.
/// </summary>
public class HttpRouter
{
    private readonly ConcurrentDictionary<string, Func<HttpContext, Task<HttpResult>>> _routeHandlers = new();

    /// <summary>
    /// Registers a controller and its routes to the router.
    /// </summary>
    /// <typeparam name="T">The type of the controller to register.</typeparam>
    public void RegisterController<T>() where T : HttpController, new()
    {
        var controllerType = typeof(T);

        // Ensure the controller is decorated with ApiControllerAttribute
        if (!controllerType.IsDefined(typeof(ApiControllerAttribute), false))
            throw new InvalidOperationException($"Controller {controllerType.Name} must be decorated with [ApiControllerAttribute].");

        var controllerInstance = new T();
        var methods = controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance);

        foreach (var method in methods)
        {
            var routeAttribute = method.GetCustomAttribute<HttpRouteAttribute>();
            if (routeAttribute == null) continue;

            string routeKey = $"{routeAttribute.Method}:{routeAttribute.Path}";

            if (_routeHandlers.ContainsKey(routeKey))
                throw new InvalidOperationException($"Duplicate route found: {routeKey}");

            _routeHandlers[routeKey] = async context =>
            {
                // Validate method parameters
                var parameters = method.GetParameters();
                if (parameters.Length != 1 || parameters[0].ParameterType != typeof(HttpContext))
                    throw new InvalidOperationException($"Method {method.Name} in {controllerType.Name} must accept a single HttpContext parameter.");

                // Invoke the method and return the result
                var result = method.Invoke(controllerInstance, [context]);
                if (result is Task<HttpResult> taskResult)
                    return await taskResult;

                throw new InvalidOperationException($"Method {method.Name} in {controllerType.Name} must return Task<HttpResult>.");
            };
        }
    }

    /// <summary>
    /// Routes the HTTP request to the corresponding handler.
    /// </summary>
    /// <param name="context">The HTTP context containing the request and response.</param>
    /// <returns>The result of processing the route.</returns>
    public async Task<HttpResult> RouteAsync(HttpContext context)
    {
        string routeKey = $"{context.Request.HttpMethod}:{context.Request.Url?.AbsolutePath}";

        if (_routeHandlers.TryGetValue(routeKey, out var handler))
        {
            return await handler(context);
        }

        return HttpResult.Fail("Route not found");
    }
}