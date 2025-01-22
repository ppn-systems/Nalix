using Notio.Network.Http.Attributes;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;

namespace Notio.Network.Http;

/// <summary>
/// Handles routing of HTTP requests to registered controllers and their methods.
/// </summary>
internal sealed class HttpRouter
{
    private readonly ConcurrentDictionary<string, Func<HttpContext, Task>> _routeHandlers = new();

    /// <summary>
    /// Registers a controller and its routes to the router.
    /// </summary>
    /// <typeparam name="T">The type of the controller to register.</typeparam>
    public void RegisterController<T>() where T : class, new()
    {
        Type controllerType = typeof(T);

        if (!controllerType.IsDefined(typeof(ApiControllerAttribute), false))
            throw new InvalidOperationException($"Controller {controllerType.Name} must be decorated with [ApiControllerAttribute].");

        T controllerInstance = new();
        MethodInfo[] methods = controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

        foreach (MethodInfo method in methods)
        {
            RouteAttribute? routeAttribute = method.GetCustomAttribute<RouteAttribute>();
            if (routeAttribute == null) continue;

            string routeKey = $"{routeAttribute.Method.ToString().ToUpper()}:{routeAttribute.Path}";

            if (_routeHandlers.ContainsKey(routeKey))
                throw new InvalidOperationException($"Duplicate route found: {routeKey}");

            _routeHandlers[routeKey] = async context =>
            {
                var parameters = method.GetParameters();
                if (parameters.Length != 1 || parameters[0].ParameterType != typeof(HttpContext))
                    throw new InvalidOperationException(
                        $"Method {method.Name} in {controllerType.Name} must accept a single HttpContext parameter.");

                try
                {
                    if (method.Invoke(controllerInstance, [context]) is Task task)
                    {
                        await task;
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            $"Method {method.Name} in {controllerType.Name} did not return a Task.");
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Error invoking method {method.Name} in {controllerType.Name}: {ex.Message}", ex);
                }
            };
        }
    }

    /// <summary>
    /// Routes the HTTP request to the corresponding handler.
    /// </summary>
    /// <param name="context">The HTTP context containing the request and response.</param>
    /// <returns>The result of processing the route.</returns>
    public async Task RouteAsync(HttpContext context)
    {
        string routeKey = $"{context.Request.HttpMethod.ToUpper()}:{context.Request.Url?.AbsolutePath}";

        if (routeKey != null && _routeHandlers.TryGetValue(routeKey, out Func<HttpContext, Task>? handler))
        {
            await handler(context);
        }
        else
        {
            object notFoundResponse = new
            {
                StatusCode = (int)HttpStatusCode.NotFound,
                Error = "Route not found",
                Message = $"No route matches path: {context.Request.Url?.AbsolutePath} and method: {context.Request.HttpMethod}"
            };

            await context.Response.WriteErrorResponseAsync(HttpStatusCode.NotFound, notFoundResponse);
        }
    }
}