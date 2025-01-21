using System;

namespace Notio.Http.Attributes;

public enum HttpMethodType
{
    GET,
    POST,
    PUT,
    DELETE,
    PATCH,
    OPTIONS,
    HEAD
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class RouteAttribute(string path, HttpMethodType method = HttpMethodType.GET) : Attribute
{
    public string Path { get; } = path;
    public HttpMethodType Method { get; } = method;
}