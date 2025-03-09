using Notio.Serialization;
using System;
using System.Collections.Generic;

namespace Notio.Network.Package.Utilities;

public static class DictionaryConverter
{
    /// <summary>
    /// Serializes a Dictionary<object, object> into a JSON string.
    /// </summary>
    public static string Serialize(Dictionary<object, object> dictionary, bool format = false)
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        return Json.Serialize(dictionary, format);
    }

    /// <summary>
    /// Deserializes a JSON string into a Dictionary<object, object>.
    /// </summary>
    public static Dictionary<object, object> Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        var result = Json.Deserialize(json);
        return result is Dictionary<string, object> dict
            ? ConvertKeysToObject(dict)
            : throw new InvalidOperationException("Deserialized data is not a valid dictionary.");
    }

    /// <summary>
    /// Chuyển đổi Dictionary<string, object> thành Dictionary<object, object>.
    /// </summary>
    private static Dictionary<object, object> ConvertKeysToObject(Dictionary<string, object> input)
    {
        var output = new Dictionary<object, object>();
        foreach (var kvp in input)
        {
            output[kvp.Key] = kvp.Value;
        }
        return output;
    }
}
