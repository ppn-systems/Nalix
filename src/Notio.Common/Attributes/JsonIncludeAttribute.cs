using System;

namespace Notio.Common.Attributes;

/// <summary>
/// Attribute to indicate that a property should be included in JSON serialization.
/// </summary>
/// <seealso cref="Attribute" />
/// <remarks>
/// Initializes a new instance of the <see cref="JsonIncludeAttribute" /> class.
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public class JsonIncludeAttribute : Attribute
{
}
