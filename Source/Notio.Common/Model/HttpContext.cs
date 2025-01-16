using System.Collections.Generic;
using System.Net;

namespace Notio.Common.Model;

/// <summary>
/// Represents an HTTP context.
/// </summary>
public class HttpContext(HttpListenerContext context)
{

    /// <summary>
    /// Gets the HTTP listener request.
    /// </summary>
    public HttpListenerRequest Request { get; } = context.Request;

    /// <summary>
    /// Gets the HTTP listener response.
    /// </summary>
    public HttpListenerResponse Response { get; } = context.Response;

    /// <summary>
    /// Gets the route parameters.
    /// </summary>
    public Dictionary<string, string> RouteParams { get; } = [];
}
