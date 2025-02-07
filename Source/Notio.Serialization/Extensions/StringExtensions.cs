using System;

namespace Notio.Serialization.Extensions;

/// <summary>
/// String related extension methods.
/// </summary>
internal static class StringExtensions
{
    /// <summary>
    /// Returns a string that represents the given item
    /// It tries to use InvariantCulture if the ToString(IFormatProvider)
    /// overload exists.
    /// </summary>
    /// <param name="value">The item.</param>
    /// <returns>A <see cref="string" /> that represents the current object.</returns>
    internal static string ToStringInvariant(this object? value) =>
        value switch
        {
            null => string.Empty,
            string s => s,
            _ => Definitions.BasicTypesInfo.Value.TryGetValue(value.GetType(), out var info)
                ? info.ToStringInvariant(value)
                : value.ToString() ?? string.Empty
        };

    /// <summary>
    /// Returns a string that represents the given item
    /// It tries to use InvariantCulture if the ToString(IFormatProvider)
    /// overload exists.
    /// </summary>
    /// <typeparam name="T">The type to get the string.</typeparam>
    /// <param name="item">The item.</param>
    /// <returns>A <see cref="string" /> that represents the current object.</returns>
    internal static string ToStringInvariant<T>(this T item)
        => typeof(string) == typeof(T) ? item as string
        ?? string.Empty : ToStringInvariant(item as object)
        ?? string.Empty;

    /// <summary>
    /// Retrieves a section of the string, inclusive of both, the start and end indexes.
    /// This behavior is unlike JavaScript's Slice behavior where the end index is non-inclusive
    /// If the string is null it returns an empty string.
    /// </summary>
    /// <param name="value">The string.</param>
    /// <param name="startIndex">The start index.</param>
    /// <param name="endIndex">The end index.</param>
    /// <returns>Retrieves a substring from this instance.</returns>
    internal static string Slice(this string? value, int startIndex, int endIndex)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        startIndex = Math.Clamp(startIndex, 0, value.Length - 1);
        endIndex = Math.Clamp(endIndex, startIndex, value.Length - 1);

        return value[startIndex..(endIndex + 1)];
    }

    /// <summary>
    /// Gets a part of the string clamping the length and startIndex parameters to safe values.
    /// If the string is null it returns an empty string. This is basically just a safe version
    /// of string.Substring.
    /// </summary>
    /// <param name="value">The string.</param>
    /// <param name="startIndex">The start index.</param>
    /// <param name="length">The length.</param>
    /// <returns>Retrieves a substring from this instance.</returns>
    internal static string SliceLength(this string? value, int startIndex, int length)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        startIndex = Math.Clamp(startIndex, 0, value.Length - 1);
        length = Math.Clamp(length, 0, value.Length - startIndex);

        return length == 0 ? string.Empty : value.Substring(startIndex, length);
    }

    /// <summary>
    /// Gets the line and column number (i.e. not index) of the
    /// specified character index. Useful to locate text in a multi-line
    /// string the same way a text editor does.
    /// Please not that the tuple contains first the line number and then the
    /// column number.
    /// </summary>
    /// <param name="value">The string.</param>
    /// <param name="charIndex">Index of the character.</param>
    /// <returns>A 2-tuple whose value is (item1, item2).</returns>
    internal static (int Line, int Column) TextPositionAt(this string? value, int charIndex)
    {
        if (string.IsNullOrEmpty(value)) return (0, 0);

        charIndex = Math.Clamp(charIndex, 0, value.Length - 1);

        int line = 1, column = 1;
        for (int i = 0; i < charIndex; i++)
        {
            switch (value[i])
            {
                case '\n': line++; column = 1; break;
                case '\r': break;
                default: column++; break;
            }
        }

        return (line, column);
    }
}