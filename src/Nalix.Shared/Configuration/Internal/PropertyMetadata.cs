using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Nalix.Shared.Configuration.Internal;

/// <summary>
/// Stores metadata about a configuration property.
/// </summary>
internal class PropertyMetadata
{
    #region Properties

    /// <summary>
    /// Gets or sets the property information.
    /// </summary>
    public PropertyInfo PropertyInfo { get; init; } = null!;

    /// <summary>
    /// Gets or sets the name of the property.
    /// </summary>
    public string Name { get; init; } = null!;

    /// <summary>
    /// Gets or sets the type of the property.
    /// </summary>
    public Type PropertyType { get; init; } = null!;

    /// <summary>
    /// Gets or sets the type code of the property.
    /// </summary>
    public TypeCode TypeCode { get; init; }

    #endregion Properties

    #region Public Methods

    /// <summary>
    /// Sets the value of this property on the specified target object.
    /// </summary>
    /// <param name="target">The target object.</param>
    /// <param name="value">The value to set.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetValue(object target, object? value)
    {
        // Only set if the types are compatible
        if (value == null || PropertyType.IsInstanceOfType(value))
        {
            PropertyInfo.SetValue(target, value);
        }
        else
        {
            throw new InvalidOperationException(
                $"Type mismatch for property {Name}: " +
                $"Expected {PropertyType}, but got {value.GetType()}");
        }
    }

    #endregion Public Methods
}
