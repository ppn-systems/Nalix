using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Notio.Serialization.Extensions;

/// <summary>
/// Provides various extension methods for Reflection and Types.
/// </summary>
internal static class ReflectionExtensions
{
    /// <summary>
    /// The closest programmatic equivalent of default(T).
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>
    /// Default value of this type.
    /// </returns>
    /// <exception cref="ArgumentNullException">type.</exception>
    internal static object? GetDefault(this Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return type.IsValueType ? Activator.CreateInstance(type) : default;
    }

    /// <summary>
    /// Tries to parse using the basic types.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <param name="value">The value.</param>
    /// <param name="result">The result.</param>
    /// <returns>
    ///   <c>true</c> if parsing was successful; otherwise, <c>false</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">type</exception>
    internal static bool TryParseBasicType(this Type type, object value, out object? result)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (type != typeof(bool))
            return type.TryParseBasicType(value.ToStringInvariant() ?? string.Empty, out result);

        result = value.ToBoolean();
        return true;
    }

    /// <summary>
    /// Tries to parse using the basic types.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <param name="value">The value.</param>
    /// <param name="result">The result.</param>
    /// <returns>
    ///   <c>true</c> if parsing was successful; otherwise, <c>false</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">type</exception>
    internal static bool TryParseBasicType(this Type type, string value, out object? result)
    {
        ArgumentNullException.ThrowIfNull(type);

        result = null;

        return Definitions.BasicTypesInfo.Value.ContainsKey(type)
            && Definitions.BasicTypesInfo.Value[type].TryParse(value, out result);
    }

    /// <summary>
    /// Tries the type of the set basic value to a property.
    /// </summary>
    /// <param name="propertyInfo">The property information.</param>
    /// <param name="value">The value.</param>
    /// <param name="target">The object.</param>
    /// <returns>
    ///   <c>true</c> if parsing was successful; otherwise, <c>false</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">propertyInfo.</exception>
    internal static bool TrySetBasicType(this PropertyInfo propertyInfo, object value, object target)
    {
        ArgumentNullException.ThrowIfNull(propertyInfo);

        try
        {
            if (propertyInfo.PropertyType.TryParseBasicType(value, out var propertyValue))
            {
                propertyInfo.SetValue(target, propertyValue);
                return true;
            }
        }
        catch
        {
            // swallow
        }

        return false;
    }

    /// <summary>
    /// Tries the type of the set to an array a basic type.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <param name="value">The value.</param>
    /// <param name="target">The array.</param>
    /// <param name="index">The index.</param>
    /// <returns>
    ///   <c>true</c> if parsing was successful; otherwise, <c>false</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">type</exception>
    internal static bool TrySetArrayBasicType(this Type type, object value, Array target, int index)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (target == null)
            return false;

        try
        {
            if (value == null)
            {
                target.SetValue(null, index);
                return true;
            }

            if (type.TryParseBasicType(value, out var propertyValue))
            {
                target.SetValue(propertyValue, index);
                return true;
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                target.SetValue(null, index);
                return true;
            }
        }
        catch
        {
            // swallow
        }

        return false;
    }

    /// <summary>
    /// Tries to set a property array with another array.
    /// </summary>
    /// <param name="propertyInfo">The property.</param>
    /// <param name="value">The value.</param>
    /// <param name="obj">The object.</param>
    /// <returns>
    ///   <c>true</c> if parsing was successful; otherwise, <c>false</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">propertyInfo.</exception>
    internal static bool TrySetArray(this PropertyInfo propertyInfo, IEnumerable<object> value, object obj)
    {
        ArgumentNullException.ThrowIfNull(propertyInfo);

        var elementType = propertyInfo.PropertyType.GetElementType();

        if (elementType == null || value == null)
            return false;

        var targetArray = Array.CreateInstance(elementType, value.Count());

        var i = 0;

        foreach (var sourceElement in value)
        {
            var result = elementType.TrySetArrayBasicType(sourceElement, targetArray, i++);

            if (!result) return false;
        }

        propertyInfo.SetValue(obj, targetArray);

        return true;
    }

    /// <summary>
    /// Convert a string to a boolean.
    /// </summary>
    /// <param name="str">The string.</param>
    /// <returns>
    ///   <c>true</c> if the string represents a valid truly value, otherwise <c>false</c>.
    /// </returns>
    internal static bool ToBoolean(this string str)
    {
        try
        {
            return Convert.ToBoolean(str);
        }
        catch (FormatException)
        {
            // ignored
        }

        try
        {
            return Convert.ToBoolean(Convert.ToInt32(str));
        }
        catch
        {
            // ignored
        }

        return false;
    }

    /// <summary>
    /// Convert a object to a boolean.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>
    ///   <c>true</c> if the string represents a valid truly value, otherwise <c>false</c>.
    /// </returns>
    internal static bool ToBoolean(this object value) => value.ToStringInvariant()?.ToBoolean() ?? false;
}