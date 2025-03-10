using System;

namespace Notio.Common.Attributes;

/// <summary>
/// Attribute to indicate that a property should be included in JSON serialization.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public class JsonIncludeAttribute : Attribute
{
}
