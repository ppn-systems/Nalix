using System.Collections.Generic;
using System.Net;

namespace Notio.Network.Https;

public class NotioHttpsContext(HttpListenerContext context)
{
    public HttpListenerRequest Request { get; } = context.Request;
    public HttpListenerResponse Response { get; } = context.Response;
    public Dictionary<string, string> RouteParams { get; } = [];
}