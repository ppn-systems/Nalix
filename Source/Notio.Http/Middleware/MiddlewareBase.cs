using System.Threading.Tasks;
using Notio.Http.Core;

namespace Notio.Http.Middleware;

/// <summary>
/// Defines middleware that can be added to the application's request pipeline.
/// </summary>
public interface IMiddleware
{
    /// <summary>
    /// Request handling method.
    /// </summary>
    /// <param name="context">The <see cref="HttpContext"/> for the current request.</param>
    /// <returns>A <see cref="Task"/> that represents the execution of this middleware.</returns>
    Task InvokeAsync(HttpContext context);
}

public abstract class MiddlewareBase : IMiddleware
{
    private MiddlewareBase _next;

    public void SetNext(MiddlewareBase next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        await HandleAsync(context);
        if (_next != null) await _next.InvokeAsync(context);
    }

    protected abstract Task HandleAsync(HttpContext context);
}