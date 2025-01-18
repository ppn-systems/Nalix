using System;

namespace Notio.Http.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public sealed class RouteAttribute(string path, string method = "GET") : Attribute
{
    public string Path { get; } = path;
    public string Method { get; } = method.ToUpper();
}