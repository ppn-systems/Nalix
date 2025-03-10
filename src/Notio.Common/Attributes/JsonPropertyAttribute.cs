using System;

namespace Notio.Common.Attributes;

/// <summary>
/// An attribute used to help setup a property behavior when serialize/deserialize JSON.
/// </summary>
/// <seealso cref="Attribute" />
/// <remarks>
/// Initializes a new instance of the <see cref="JsonPropertyAttribute" /> class.
/// </remarks>
/// <param name="propertyName">Name of the property.</param>
[AttributeUsage(AttributeTargets.Property)]
public sealed class JsonPropertyAttribute(string propertyName) : Attribute
{
    /// <summary>
    /// Gets or sets the name of the property.
    /// </summary>
    /// <value>
    /// The name of the property.
    /// </value>
    public string PropertyName { get; } = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
}
