using Notio.Lite.Formatters;
using Notio.Lite.Reflection;
using System;
using System.Text.RegularExpressions;

namespace Notio.Lite.Extensions;

/// <summary>
/// String related extension methods.
/// </summary>
public static class StringExtensions
{
    #region Private Declarations

    private const RegexOptions StandardRegexOptions =
        RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.CultureInvariant;

    private static readonly Lazy<Regex> SplitLinesRegex =
        new(() => new("\r\n|\r|\n", StandardRegexOptions));

    private static readonly Lazy<Regex> UnderscoreRegex =
        new(() => new(@"_", StandardRegexOptions));

    private static readonly Lazy<Regex> CamelCaseRegEx =
        new(() => new(@"[a-z][A-Z]", StandardRegexOptions));

    private static readonly Lazy<MatchEvaluator> SplitCamelCaseString = new Lazy<MatchEvaluator>(() => m =>
    {
        var x = m.ToString();
        return x[0] + " " + x[1..];
    });

    #endregion Private Declarations

    /// <summary>
    /// Returns a string that represents the given item
    /// It tries to use InvariantCulture if the ToString(IFormatProvider)
    /// overload exists.
    /// </summary>
    /// <param name="this">The item.</param>
    /// <returns>A <see cref="string" /> that represents the current object.</returns>
    public static string? ToStringInvariant(this object? @this)
    {
        if (@this == null)
            return string.Empty;

        var itemType = @this.GetType();

        if (itemType == typeof(string))
            return @this as string ?? string.Empty;

        return Definitions.BasicTypesInfo.Value.TryGetValue(itemType, out ExtendedTypeInfo? value)
            ? value.ToStringInvariant(@this)
            : @this.ToString();
    }

    /// <summary>
    /// Returns a string that represents the given item
    /// It tries to use InvariantCulture if the ToString(IFormatProvider)
    /// overload exists.
    /// </summary>
    /// <typeparam name="T">The type to get the string.</typeparam>
    /// <param name="item">The item.</param>
    /// <returns>A <see cref="string" /> that represents the current object.</returns>
    public static string ToStringInvariant<T>(this T item)
        => typeof(string) == typeof(T) ? item as string
        ?? string.Empty : ToStringInvariant(item as object)
        ?? string.Empty;

    /// <summary>
    /// Returns text representing the properties of the specified object in a human-readable format.
    /// While this method is fairly expensive computationally speaking, it provides an easy way to
    /// examine objects.
    /// </summary>
    /// <param name="this">The object.</param>
    /// <returns>A <see cref="string" /> that represents the current object.</returns>
    public static string Stringify(this object @this)
    {
        if (@this == null)
            return "(null)";

        try
        {
            var jsonText = Json.Serialize(@this, false, "$type");
            var jsonData = Json.Deserialize(jsonText);

            return new HumanizeJson(jsonData, 0).GetResult();
        }
        catch
        {
            return @this.ToStringInvariant() ?? string.Empty;
        }
    }

    /// <summary>
    /// Retrieves a section of the string, inclusive of both, the start and end indexes.
    /// This behavior is unlike JavaScript's Slice behavior where the end index is non-inclusive
    /// If the string is null it returns an empty string.
    /// </summary>
    /// <param name="this">The string.</param>
    /// <param name="startIndex">The start index.</param>
    /// <param name="endIndex">The end index.</param>
    /// <returns>Retrieves a substring from this instance.</returns>
    public static string Slice(this string @this, int startIndex, int endIndex)
    {
        if (@this == null)
            return string.Empty;

        var end = Math.Clamp(endIndex, startIndex, @this.Length - 1);
        return startIndex >= end ? string.Empty : @this.Substring(startIndex, (end - startIndex) + 1);
    }

    /// <summary>
    /// Gets a part of the string clamping the length and startIndex parameters to safe values.
    /// If the string is null it returns an empty string. This is basically just a safe version
    /// of string.Substring.
    /// </summary>
    /// <param name="this">The string.</param>
    /// <param name="startIndex">The start index.</param>
    /// <param name="length">The length.</param>
    /// <returns>Retrieves a substring from this instance.</returns>
    public static string SliceLength(this string @this, int startIndex, int length)
    {
        if (@this == null)
            return string.Empty;

        var start = Math.Clamp(startIndex, 0, @this.Length - 1);
        var len = Math.Clamp(length, 0, @this.Length - start);

        return len == 0 ? string.Empty : @this.Substring(start, len);
    }

    /// <summary>
    /// Splits the specified text into r, n or rn separated lines.
    /// </summary>
    /// <param name="this">The text.</param>
    /// <returns>
    /// An array whose elements contain the substrings from this instance
    /// that are delimited by one or more characters in separator.
    /// </returns>
    public static string[] ToLines(this string @this) =>
        @this == null ? [] : SplitLinesRegex.Value.Split(@this);

    /// <summary>
    /// Humanizes (make more human-readable) an identifier-style string
    /// in either camel case or snake case. For example, CamelCase will be converted to
    /// Camel Case and Snake_Case will be converted to Snake Case.
    /// </summary>
    /// <param name="value">The identifier-style string.</param>
    /// <returns>A <see cref="string" /> humanized.</returns>
    public static string Humanize(this string value)
    {
        if (value == null)
            return string.Empty;

        var returnValue = UnderscoreRegex.Value.Replace(value, " ");
        returnValue = CamelCaseRegEx.Value.Replace(returnValue, SplitCamelCaseString.Value);
        return returnValue;
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
    public static Tuple<int, int> TextPositionAt(this string value, int charIndex)
    {
        if (value == null)
            return Tuple.Create(0, 0);

        var index = Math.Clamp(charIndex, 0, value.Length - 1);

        var lineIndex = 0;
        var colNumber = 0;

        for (var i = 0; i <= index; i++)
        {
            if (value[i] == '\n')
            {
                lineIndex++;
                colNumber = 0;
                continue;
            }

            if (value[i] != '\r')
                colNumber++;
        }

        return Tuple.Create(lineIndex + 1, colNumber);
    }
}