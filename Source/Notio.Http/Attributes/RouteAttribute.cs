using Notio.Http.Enums;
using System;

namespace Notio.Http.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public sealed class RouteAttribute(string path, HttpMethod method = HttpMethod.Get) : Attribute
{
    public string Path { get; } = path;
    public HttpMethod Method { get; } = method;
}