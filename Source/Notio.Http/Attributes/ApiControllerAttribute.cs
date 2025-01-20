using System;

namespace Notio.Http.Attributes;

/// <summary>
/// Marks a class as an API Controller for routing purposes.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class ApiControllerAttribute : Attribute
{
    // Currently empty, but provides extensibility for future use.
}