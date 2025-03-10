using Notio.Serialization;
using System;
using System.Collections.Generic;
using System.Text;

namespace Notio.Network.Package.Utilities;

/// <summary>
/// Provides utility methods for serializing and deserializing a <see cref="Dictionary{TKey, TValue}"/> 
/// between JSON format and UTF-8 encoded byte arrays.
/// </summary>
public static class DictionaryUtils
{
    /// <summary>
    /// Serializes a <see cref="Dictionary{TKey, TValue}"/> with string keys and object values into a UTF-8 encoded JSON byte array.
    /// </summary>
    /// <param name="dictionary">The dictionary to serialize.</param>
    /// <param name="format">If set to <c>true</c>, the output JSON will be formatted with indentation.</param>
    /// <returns>A byte array containing the JSON representation of the dictionary, encoded in UTF-8.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dictionary"/> is <c>null</c>.</exception>
    public static byte[] Serialize(Dictionary<string, object> dictionary, bool format = false)
    {
        ArgumentNullException.ThrowIfNull(dictionary);

        string json = Json.Serialize(dictionary, format);
        return Encoding.UTF8.GetBytes(json);
    }

    /// <summary>
    /// Deserializes a UTF-8 encoded JSON byte array into a <see cref="Dictionary{TKey, TValue}"/> with string keys and object values.
    /// </summary>
    /// <param name="json">The UTF-8 encoded JSON byte array.</param>
    /// <returns>A <see cref="Dictionary{TKey, TValue}"/> containing the deserialized key-value pairs.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="json"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the deserialized data is not a valid dictionary.</exception>
    public static Dictionary<string, object> Deserialize(byte[] json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return Deserialize(json.AsSpan());
    }

    /// <summary>
    /// Deserializes a UTF-8 encoded JSON span into a <see cref="Dictionary{TKey, TValue}"/> with string keys and object values.
    /// </summary>
    /// <param name="json">The UTF-8 encoded JSON data as a <see cref="ReadOnlySpan{T}"/>.</param>
    /// <returns>A <see cref="Dictionary{TKey, TValue}"/> containing the deserialized key-value pairs.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="json"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the deserialized data is not a valid dictionary.</exception>
    public static Dictionary<string, object> Deserialize(ReadOnlySpan<byte> json)
    {
        if (json.IsEmpty)
            throw new ArgumentException("JSON byte span cannot be empty.", nameof(json));

        string jsonString = Encoding.UTF8.GetString(json);
        var result = Json.Deserialize(jsonString);

        return result as Dictionary<string, object>
            ?? throw new InvalidOperationException("Deserialized data is not a valid dictionary.");
    }
}
