using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Nalix.Serialization;

/// <summary>
/// Provides optimized JSON serialization and deserialization methods, supporting both strings and byte arrays.
/// </summary>
public static class JsonCodec
{
    /// <summary>
    /// Serializes an object to a JSON string using the specified type metadata.
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize.</typeparam>
    /// <param name="obj">The object to serialize.</param>
    /// <param name="jsonTypeInfo">The metadata used for JSON serialization. Must not be <see langword="null"/>.</param>
    /// <returns>A JSON string representation of the object.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="jsonTypeInfo"/> is <see langword="null"/>.</exception>
    public static string Serialize<T>(T obj, JsonTypeInfo<T> jsonTypeInfo)
    {
        ArgumentNullException.ThrowIfNull(jsonTypeInfo);
        return JsonSerializer.Serialize(obj, jsonTypeInfo);
    }

    /// <summary>
    /// Serializes an object to a JSON byte array (UTF-8 encoded).
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize.</typeparam>
    /// <param name="obj">The object to serialize.</param>
    /// <param name="jsonTypeInfo">The metadata used for JSON serialization. Must not be <see langword="null"/>.</param>
    /// <param name="encoding">The encoding used for serialization. Environment to UTF-8.</param>
    /// <returns>A byte array containing the UTF-8 encoded JSON representation of the object.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="jsonTypeInfo"/> is <see langword="null"/>.</exception>
    public static byte[] SerializeToBytes<T>(
        T obj,
        JsonTypeInfo<T> jsonTypeInfo,
        Encoding encoding = null)
    {
        ArgumentNullException.ThrowIfNull(jsonTypeInfo);
        encoding ??= JsonOptions.Encoding;
        return encoding.GetBytes(Serialize(obj, jsonTypeInfo));
    }

    /// <summary>
    /// Serializes an object to a JSON byte array (UTF-8 encoded) using a <c>Caching&lt;byte&gt;</c>.
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize.</typeparam>
    /// <param name="obj">The object to serialize.</param>
    /// <param name="jsonTypeInfo">The metadata used for JSON serialization. Must not be <see langword="null"/>.</param>
    /// <returns>A <c>Caching&lt;byte&gt;</c> containing the UTF-8 encoded JSON representation of the object.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="jsonTypeInfo"/> is <see langword="null"/>.</exception>
    public static Memory<byte> SerializeToMemory<T>(
        T obj,
        JsonTypeInfo<T> jsonTypeInfo)
    {
        ArgumentNullException.ThrowIfNull(jsonTypeInfo, nameof(jsonTypeInfo));

        using MemoryStream ms = new();
        using Utf8JsonWriter writer = new(ms);
        JsonSerializer.Serialize(writer, obj, jsonTypeInfo);
        writer.Flush();
        return ms.ToArray().AsMemory();
    }

    /// <summary>
    /// Deserializes a JSON string into an object.
    /// </summary>
    /// <typeparam name="T">The target type of the deserialization.</typeparam>
    /// <param name="json">The JSON string to deserialize. Must not be <see langword="null"/>.</param>
    /// <param name="jsonTypeInfo">The metadata used for JSON deserialization. Must not be <see langword="null"/>.</param>
    /// <returns>An instance of <typeparamref name="T"/> if successful; otherwise, <see langword="default"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="json"/> or <paramref name="jsonTypeInfo"/> is <see langword="null"/>.</exception>
    public static T Deserialize<T>(string json, JsonTypeInfo<T> jsonTypeInfo)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(jsonTypeInfo);
        return JsonSerializer.Deserialize(json, jsonTypeInfo);
    }

    /// <summary>
    /// Deserializes a JSON byte array into an object.
    /// </summary>
    /// <typeparam name="T">The target type of the deserialization.</typeparam>
    /// <param name="jsonBytes">The UTF-8 encoded JSON byte array to deserialize. Must not be <see langword="null"/>.</param>
    /// <param name="jsonTypeInfo">The metadata used for JSON deserialization. Must not be <see langword="null"/>.</param>
    /// <param name="encoding">The encoding used for deserialization. Environment to UTF-8.</param>
    /// <returns>An instance of <typeparamref name="T"/> if successful; otherwise, <see langword="default"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="jsonBytes"/> or <paramref name="jsonTypeInfo"/> is <see langword="null"/>.</exception>
    public static T DeserializeFromBytes<T>(
        byte[] jsonBytes,
        JsonTypeInfo<T> jsonTypeInfo,
        Encoding encoding = null)
    {
        ArgumentNullException.ThrowIfNull(jsonBytes);
        ArgumentNullException.ThrowIfNull(jsonTypeInfo);

        encoding ??= JsonOptions.Encoding;
        return Deserialize(encoding.GetString(jsonBytes), jsonTypeInfo);
    }

    /// <summary>
    /// Deserializes a JSON payload from a <c>ReadOnlySpan&lt;byte&gt;</c> without extra allocations.
    /// </summary>
    /// <typeparam name="T">The target type of the deserialization.</typeparam>
    /// <param name="jsonBytes">The UTF-8 encoded JSON byte span to deserialize.</param>
    /// <param name="jsonTypeInfo">The metadata used for JSON deserialization. Must not be <see langword="null"/>.</param>
    /// <returns>An instance of <typeparamref name="T"/> if successful; otherwise, <see langword="default"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="jsonTypeInfo"/> is <see langword="null"/>.</exception>
    public static T DeserializeFromBytes<T>(ReadOnlySpan<byte> jsonBytes, JsonTypeInfo<T> jsonTypeInfo)
    {
        ArgumentNullException.ThrowIfNull(jsonTypeInfo);
        Utf8JsonReader reader = new(jsonBytes);
        return JsonSerializer.Deserialize(ref reader, jsonTypeInfo);
    }
}
