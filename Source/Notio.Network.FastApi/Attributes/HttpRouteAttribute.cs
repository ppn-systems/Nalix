using Notio.Network.FastApi.Enums;
using System;

namespace Notio.Network.FastApi.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public class HttpRouteAttribute(string path, HttpMethodType method) : Attribute
{
    public string Path { get; } = path;
    public HttpMethodType Method { get; } = method;
}