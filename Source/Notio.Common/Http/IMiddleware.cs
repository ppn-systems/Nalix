using Notio.Common.Model;
using System.Threading.Tasks;

namespace Notio.Common.Http;

/// <summary>
/// Represents a middleware interface.
/// </summary>
public interface IMiddleware
{
    /// <summary>
    /// Invokes the middleware asynchronously.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task InvokeAsync(HttpContext context);
}
