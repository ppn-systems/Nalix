using Nalix.Common.Package.Attributes;
using Nalix.Common.Package.Enums;
using Nalix.Serialization;
using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace Nalix.Shared.Net.Handlers;

/// <summary>
/// Provides human-readable messages associated with <see cref="PacketCode"/> values,
/// supporting both string and UTF-8 byte formats.
/// </summary>
public static class PacketCodeMap
{
    // Thread-safe cache for storing the messages
    private static readonly ConcurrentDictionary<PacketCode, string> MessageCache = new();

    private static readonly ConcurrentDictionary<PacketCode, byte[]> MessageCacheBytes = new();

    // Static constructor to initialize the cache
    static PacketCodeMap()
    {
        foreach (PacketCode code in Enum.GetValues<PacketCode>())
        {
            // Get the message using the custom attribute or default message
            string message = GetMessageFromAttribute(code) ?? $"No message available for {code}."; ;

            // Cache both the string message and the byte[] version
            MessageCache[code] = message;
            MessageCacheBytes[code] = JsonOptions.Encoding.GetBytes(message); // Using UTF8 encoding directly
        }
    }

    /// <summary>
    /// Gets the human-readable string message associated with a <see cref="PacketCode"/>.
    /// </summary>
    /// <param name="code">The <see cref="PacketCode"/> value to look up.</param>
    /// <returns>The associated message string.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no message is found (should not occur).</exception>
    public static string GetMessage(PacketCode code)
    {
        // Try to get the message from the cache
        if (MessageCache.TryGetValue(code, out string? cached))
            return cached;

        // This point should never be reached due to static constructor initialization
        throw new InvalidOperationException($"No message available for {code}.");
    }

    /// <summary>
    /// Gets the message for a <see cref="PacketCode"/> as a UTF-8 encoded <see cref="byte"/> array.
    /// </summary>
    /// <param name="code">The <see cref="PacketCode"/> value.</param>
    /// <returns>UTF-8 encoded byte array of the message.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no message is found (should not occur).</exception>
    public static byte[] GetMessageBytes(PacketCode code)
    {
        // Try to get the message from the cache
        if (MessageCacheBytes.TryGetValue(code, out byte[]? cached))
            return cached;

        // This point should never be reached due to static constructor initialization
        throw new InvalidOperationException($"No message available for {code}.");
    }

    // Helper to retrieve the message from the custom attribute (if any)
    private static string? GetMessageFromAttribute(PacketCode code)
        => typeof(PacketCode).GetField(code.ToString())?
                             .GetCustomAttribute<PacketCodeMessageAttribute>()?
                             .Message;
}
