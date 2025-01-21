using Notio.Network.FastApi.Enums;
using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;

namespace Notio.Network.FastApi;

public class RequestProcessor(Dictionary<string, Dictionary<HttpMethodType, MethodInfo>> routes)
{
    private readonly Dictionary<string, Dictionary<HttpMethodType, MethodInfo>> _routes = routes;

    public void ProcessRequest(HttpListenerContext context)
    {
        string requestPath = context.Request.Url.AbsolutePath;

        if (Enum.TryParse(context.Request.HttpMethod, out HttpMethodType requestMethod) &&
            _routes.TryGetValue(requestPath, out Dictionary<HttpMethodType, MethodInfo> value) &&
            value.TryGetValue(requestMethod, out MethodInfo method))
        {
            try
            {
                method.Invoke(null, [context]);
            }
            catch (Exception)
            {
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                using var writer = new System.IO.StreamWriter(context.Response.OutputStream);
                writer.Write("Internal Server Error");
            }
        }
        else
        {
            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            using var writer = new System.IO.StreamWriter(context.Response.OutputStream);
            writer.Write("Route Not Found");
        }
        context.Response.Close();
    }
}