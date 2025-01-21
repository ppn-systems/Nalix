using Notio.Http.Attributes;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;

namespace Notio.Http.Core;

/// <summary>
/// Handles routing of HTTP requests to registered controllers and their methods.
/// </summary>
internal class HttpRouter
{
    private readonly ConcurrentDictionary<string, Func<HttpContext, Task<HttpResponse>>> _routeHandlers = new();

    /// <summary>
    /// Registers a controller and its routes to the router.
    /// </summary>
    /// <typeparam name="T">The type of the controller to register.</typeparam>
    public void RegisterController<T>() where T : HttpController, new()
    {
        Type controllerType = typeof(T);

        // Ensure the controller is decorated with ApiControllerAttribute
        if (!controllerType.IsDefined(typeof(ApiControllerAttribute), false))
            throw new InvalidOperationException($"Controller {controllerType.Name} must be decorated with [ApiControllerAttribute].");

        T controllerInstance = new();
        MethodInfo[] methods = controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

        foreach (MethodInfo method in methods)
        {
            RouteAttribute routeAttribute = method.GetCustomAttribute<RouteAttribute>();
            if (routeAttribute == null) continue;

            string routeKey = $"{routeAttribute.Method.ToString().ToUpper()}:{routeAttribute.Path}";

            if (_routeHandlers.ContainsKey(routeKey))
                throw new InvalidOperationException($"Duplicate route found: {routeKey}");

            _routeHandlers[routeKey] = async context =>
            {
                // Validate method parameters
                var parameters = method.GetParameters();
                if (parameters.Length != 1 || parameters[0].ParameterType != typeof(HttpContext))
                    throw new 
                    InvalidOperationException($"Method {method.Name} in {controllerType.Name} must accept a single HttpContext parameter.");

                try
                {
                    // Invoke the method and return the result
                    var result = method.Invoke(controllerInstance, [context]);
                    if (result is Task<HttpResponse> taskResult)
                        return await taskResult;

                    throw new 
                    InvalidOperationException($"Method {method.Name} in {controllerType.Name} must return Task<HttpResponse>.");
                }
                catch (Exception ex)
                {
                    // Log the exception (if necessary) or rethrow it
                    throw new 
                    InvalidOperationException($"Error invoking method {method.Name} in {controllerType.Name}: {ex.Message}", ex);
                }
            };
        }
    }

    /// <summary>
    /// Routes the HTTP request to the corresponding handler.
    /// </summary>
    /// <param name="context">The HTTP context containing the request and response.</param>
    /// <returns>The result of processing the route.</returns>
    public async Task<HttpResponse> RouteAsync(HttpContext context)
    {
        if (_routeHandlers.TryGetValue($"{context.Request.HttpMethod.ToUpper()}:{context.Request.Url?.AbsolutePath}",
            out Func<HttpContext, Task<HttpResponse>> handler)) 
            return await handler(context);
        else
        {
            return await Task.FromResult(new HttpResponse
            (
                HttpStatusCode.NotFound,
                null,
                "Route not found",
                $"No route matches path: {context.Request.Url?.AbsolutePath} and method: {context.Request.HttpMethod}"
            ));
        }
    }
}