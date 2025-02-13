using Notio.Common.Exceptions;
using Notio.Common.Package;
using System;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Notio.Network.Package.Helpers;

/// <summary>
/// Provides high-performance utility methods for the IPacket class.
/// </summary>
[SkipLocalsInit]
public static class PacketJsonHelper
{
    /// <summary>
    /// Converts a IPacket object to a JSON string.
    /// </summary>
    /// <param name="packet">The IPacket object to convert to JSON.</param>
    /// <param name="jsonTypeInfo">Options for the JsonSerializer. If not provided, default options will be used.</param>
    /// <returns>A JSON string representing the IPacket object.</returns>
    /// <exception cref="PackageException">Thrown if JSON serialization fails.</exception>
    public static string ToJson(IPacket packet, JsonTypeInfo<IPacket> jsonTypeInfo)
    {
        try
        {
            return JsonSerializer.Serialize(packet, jsonTypeInfo);
        }
        catch (Exception ex)
        {
            throw new PackageException("Failed to serialize IPacket to JSON.", ex);
        }
    }

    /// <summary>
    /// Creates a IPacket object from a JSON string.
    /// </summary>
    /// <param name="json">The JSON string representing the IPacket object.</param>
    /// <param name="jsonTypeInfo">Options for the JsonSerializer. If not provided, default options will be used.</param>
    /// <returns>The IPacket object created from the JSON string.</returns>
    /// <exception cref="PackageException">Thrown if the JSON string is invalid or deserialization fails.</exception>
    public static IPacket FromJson(string json, JsonTypeInfo<IPacket> jsonTypeInfo)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new PackageException("JSON string is null or empty.");

        try
        {
            IPacket? packet = JsonSerializer.Deserialize(json, jsonTypeInfo);
            return packet ?? throw new PackageException("Deserialized JSON is null.");
        }
        catch (Exception ex)
        {
            throw new PackageException("Failed to deserialize JSON to IPacket.", ex);
        }
    }
}
