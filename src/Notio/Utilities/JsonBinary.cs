using System;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Notio.Utilities;

/// <summary>
/// Provides optimized JSON serialization and deserialization methods, supporting both strings and byte arrays.
/// </summary>
public static class JsonBinary
{
    /// <summary>
    /// Serializes an object to a JSON string using the specified type metadata.
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize.</typeparam>
    /// <param name="obj">The object to serialize.</param>
    /// <param name="jsonTypeInfo">The metadata used for JSON serialization.</param>
    /// <returns>A JSON string representation of the object.</returns>
    public static string Serialize<T>(T obj, JsonTypeInfo<T> jsonTypeInfo)
        => JsonSerializer.Serialize(obj, jsonTypeInfo);

    /// <summary>
    /// Deserializes a JSON string into an object.
    /// </summary>
    /// <typeparam name="T">The target type of the deserialization.</typeparam>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <param name="jsonTypeInfo">The metadata used for JSON deserialization.</param>
    /// <returns>An instance of <typeparamref name="T"/> if successful; otherwise, null.</returns>
    public static T Deserialize<T>(string json, JsonTypeInfo<T> jsonTypeInfo)
        => JsonSerializer.Deserialize(json, jsonTypeInfo);

    /// <summary>
    /// Serializes an object to a JSON byte array (UTF-8 encoded).
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize.</typeparam>
    /// <param name="obj">The object to serialize.</param>
    /// <param name="jsonTypeInfo">The metadata used for JSON serialization.</param>
    /// <returns>A byte array containing the UTF-8 encoded JSON representation of the object.</returns>
    public static byte[] SerializeToBytes<T>(T obj, JsonTypeInfo<T> jsonTypeInfo)
        => OptionsDefault.Encoding.GetBytes(Serialize(obj, jsonTypeInfo));

    /// <summary>
    /// Deserializes a JSON byte array into an object.
    /// </summary>
    /// <typeparam name="T">The target type of the deserialization.</typeparam>
    /// <param name="jsonBytes">The UTF-8 encoded JSON byte array to deserialize.</param>
    /// <param name="jsonTypeInfo">The metadata used for JSON deserialization.</param>
    /// <returns>An instance of <typeparamref name="T"/> if successful; otherwise, null.</returns>
    public static T DeserializeFromBytes<T>(byte[] jsonBytes, JsonTypeInfo<T> jsonTypeInfo)
        => Deserialize(OptionsDefault.Encoding.GetString(jsonBytes), jsonTypeInfo);

    /// <summary>
    /// Deserializes a JSON payload from a <c>ReadOnlySpan&lt;byte&gt;</c> without extra allocations.
    /// </summary>
    /// <typeparam name="T">The target type of the deserialization.</typeparam>
    /// <param name="jsonBytes">The UTF-8 encoded JSON byte span to deserialize.</param>
    /// <param name="jsonTypeInfo">The metadata used for JSON deserialization.</param>
    /// <returns>An instance of <typeparamref name="T"/> if successful; otherwise, null.</returns>
    public static T DeserializeFromBytes<T>(ReadOnlySpan<byte> jsonBytes, JsonTypeInfo<T> jsonTypeInfo)
    {
        Utf8JsonReader reader = new(jsonBytes);
        return JsonSerializer.Deserialize(ref reader, jsonTypeInfo);
    }
}
