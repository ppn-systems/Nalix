using System;
using System.Net.Http;

namespace Notio.Http.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public sealed class RouteAttribute(string path, HttpMethod method = null!) : Attribute
{
    public string Path { get; } = path;
    public HttpMethod Method { get; } = method ?? HttpMethod.Get;
}