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

        if (type == typeof(bool))
        {
            result = value.ToBoolean();
            return true;
        }

        return type.TryParseBasicType(value.ToStringInvariant() ?? string.Empty, out result);
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

        if (propertyInfo.PropertyType.TryParseBasicType(value, out var parsedValue))
        {
            propertyInfo.SetValue(target, parsedValue);
            return true;
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

        object? parsedValue = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>)
            ? null
            : type.TryParseBasicType(value, out var temp) ? temp : null;

        target.SetValue(parsedValue, index);
        return true;
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

        var valueArray = value.ToArray();
        var targetArray = Array.CreateInstance(elementType, valueArray.Length);

        for (int i = 0; i < valueArray.Length; i++)
            if (!elementType.TrySetArrayBasicType(valueArray[i], targetArray, i))
                return false;

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
    internal static bool ToBoolean(this string str) =>
        bool.TryParse(str, out var boolResult) ? boolResult :
        int.TryParse(str, out var intResult) && intResult != 0;

    /// <summary>
    /// Convert a object to a boolean.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>
    ///   <c>true</c> if the string represents a valid truly value, otherwise <c>false</c>.
    /// </returns>
    internal static bool ToBoolean(this object value) =>
        value switch
        {
            bool b => b,
            int i => i != 0,
            _ => value.ToStringInvariant()?.ToBoolean() ?? false
        };
}