using System.Collections.Generic;
using System.Threading.Tasks;

namespace Notio.Network.Http.Core;

/// <summary>
/// Represents a pipeline of middlewares to process HTTP requests.
/// </summary>
public sealed class MiddlewarePipeline
{
    private readonly List<MiddlewareBase> _middlewares = new();

    /// <summary>
    /// Adds a middleware to the pipeline.
    /// </summary>
    /// <param name="middleware">The middleware to add.</param>
    public void AddMiddleware(MiddlewareBase middleware) => _middlewares.Add(middleware);

    /// <summary>
    /// Executes the middleware pipeline with the given HTTP context.
    /// </summary>
    /// <param name="context">The HTTP context to process.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ExecuteAsync(HttpContext context)
    {
        if (_middlewares.Count > 0)
        {
            MiddlewareBase first = _middlewares[0];
            for (int i = 0; i < _middlewares.Count - 1; i++)
            {
                _middlewares[i].SetNext(_middlewares[i + 1]);
            }
            await first.InvokeAsync(context);
        }
    }

    /// <summary>
    /// Shuts down the middleware pipeline gracefully.
    /// </summary>
    /// <returns>A task representing the asynchronous shutdown operation.</returns>
    public async Task ShutdownAsync()
    {
        _middlewares.Clear();
        await Task.Delay(0);
    }
}