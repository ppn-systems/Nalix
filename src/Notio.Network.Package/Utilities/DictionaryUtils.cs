using Notio.Serialization;
using System;
using System.Collections.Generic;
using System.Text;

namespace Notio.Network.Package.Utilities;

public static class DictionaryUtils
{
    /// <summary>
    /// Serializes a Dictionary<string, object> into a UTF-8 encoded JSON byte array.
    /// </summary>
    public static byte[] Serialize(Dictionary<string, object> dictionary, bool format = false)
    {
        ArgumentNullException.ThrowIfNull(dictionary);

        string json = Json.Serialize(dictionary, format);
        return Encoding.UTF8.GetBytes(json);
    }

    /// <summary>
    /// Deserializes a UTF-8 encoded JSON byte array into a Dictionary<string, object>.
    /// </summary>
    public static Dictionary<string, object> Deserialize(byte[] json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return Deserialize(json.AsSpan());
    }

    /// <summary>
    /// Deserializes a UTF-8 encoded JSON span into a Dictionary<string, object>.
    /// </summary>
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
