using System;

namespace Notio.Lite.Formatters;

/// <summary>
/// An attribute used to help setup a property behavior when serialize/deserialize JSON.
/// </summary>
/// <seealso cref="Attribute" />
/// <remarks>
/// Initializes a new instance of the <see cref="JsonPropertyAttribute" /> class.
/// </remarks>
/// <param name="propertyName">Name of the property.</param>
/// <param name="ignored">if set to <c>true</c> [ignored].</param>
[AttributeUsage(AttributeTargets.Property)]
public sealed class JsonPropertyAttribute(string propertyName, bool ignored = false) : Attribute
{
    /// <summary>
    /// Gets or sets the name of the property.
    /// </summary>
    /// <value>
    /// The name of the property.
    /// </value>
    public string PropertyName { get; } = propertyName ?? throw new ArgumentNullException(nameof(propertyName));

    /// <summary>
    /// Gets or sets a value indicating whether this <see cref="JsonPropertyAttribute" /> is ignored.
    /// </summary>
    /// <value>
    ///   <c>true</c> if ignored; otherwise, <c>false</c>.
    /// </value>
    public bool Ignored { get; } = ignored;
}