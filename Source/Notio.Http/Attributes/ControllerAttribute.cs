using System;

namespace Notio.Http.Attributes;

/// <summary>
/// Marks a class as an API Controller for routing purposes.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class ControllerAttribute : Attribute
{
    // Currently empty, but provides extensibility for future use.
}
