using System;

namespace Notio.Network.Https.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public class HttpsRouteAttribute(string path, string method = "GET") : Attribute
{
    public string Path { get; } = path;
    public string Method { get; } = method.ToUpper();
}