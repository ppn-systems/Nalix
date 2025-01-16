using System;

namespace Notio.Network.Http.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public sealed class HttpRouteAttribute(string path, string method = "GET") : Attribute
{
    public string Path { get; } = path;
    public string Method { get; } = method.ToUpper();
}