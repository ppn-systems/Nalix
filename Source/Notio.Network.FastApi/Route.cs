using Notio.Network.FastApi.Attributes;
using Notio.Network.FastApi.Enums;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Notio.Network.FastApi;

public static class Route
{
    public static Dictionary<string, Dictionary<HttpMethodType, MethodInfo>> Load()
    {
        var routes = new Dictionary<string, Dictionary<HttpMethodType, MethodInfo>>();

        var methods = Assembly.GetExecutingAssembly().GetTypes()
            .SelectMany(t => t.GetMethods())
            .Where(m => m.GetCustomAttributes(typeof(HttpRouteAttribute), false).Length > 0);

        foreach (MethodInfo method in methods)
        {
            var attribute = method.GetCustomAttribute<HttpRouteAttribute>();
            if (!routes.TryGetValue(attribute.Path, out Dictionary<HttpMethodType, MethodInfo> value))
            {
                value = [];
                routes[attribute.Path] = value;
            }

            value[attribute.Method] = method;
        }

        return routes;
    }
}