using Notio.Common.Exceptions;
using System;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Notio.Network.Package.Helpers.Type;

/// <summary>
/// Provides high-performance utility methods for the Packet class.
/// </summary>
[SkipLocalsInit]
public static class PacketJsonHelper
{
    /// <summary>
    /// Converts a Packet object to a JSON string.
    /// </summary>
    /// <param name="packet">The Packet object to convert to JSON.</param>
    /// <param name="options">Options for the JsonSerializer. If not provided, default options will be used.</param>
    /// <returns>A JSON string representing the Packet object.</returns>
    /// <exception cref="PackageException">Thrown if JSON serialization fails.</exception>
    public static string ToJson(Packet packet, JsonSerializerOptions? options = null)
    {
        try
        {
            return JsonSerializer.Serialize(packet, options ?? new JsonSerializerOptions());
        }
        catch (Exception ex)
        {
            throw new PackageException("Failed to serialize Packet to JSON.", ex);
        }
    }

    /// <summary>
    /// Creates a Packet object from a JSON string.
    /// </summary>
    /// <param name="json">The JSON string representing the Packet object.</param>
    /// <param name="options">Options for the JsonSerializer. If not provided, default options will be used.</param>
    /// <returns>The Packet object created from the JSON string.</returns>
    /// <exception cref="PackageException">Thrown if the JSON string is invalid or deserialization fails.</exception>
    public static Packet FromJson(string json, JsonSerializerOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new PackageException("JSON string is null or empty.");

        try
        {
            Packet? packet = JsonSerializer.Deserialize<Packet>(json, options ?? new JsonSerializerOptions());
            return packet ?? throw new PackageException("Deserialized JSON is null.");
        }
        catch (Exception ex)
        {
            throw new PackageException("Failed to deserialize JSON to Packet.", ex);
        }
    }
}