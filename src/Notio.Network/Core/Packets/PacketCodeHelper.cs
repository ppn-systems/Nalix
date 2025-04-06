using Notio.Common.Attributes;
using Notio.Common.Package;
using Notio.Defaults;
using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace Notio.Network.Core.Packets;

internal static class PacketCodeHelper
{
    // Thread-safe cache for storing the messages
    private static readonly ConcurrentDictionary<PacketCode, string> MessageCache = new();
    private static readonly ConcurrentDictionary<PacketCode, byte[]> MessageCacheBytes = new();

    // Static constructor to initialize the cache
    static PacketCodeHelper()
    {
        foreach (PacketCode code in Enum.GetValues<PacketCode>())
        {
            // Get the message using the custom attribute or default message
            string message = GetMessageFromAttribute(code) ?? $"No message available for {code}."; ;

            // Cache both the string message and the byte[] version
            MessageCache[code] = message;
            MessageCacheBytes[code] = DefaultConstants.DefaultEncoding.GetBytes(message); // Using UTF8 encoding directly
        }
    }

    /// <summary>
    /// Gets the message for a PacketCode.
    /// </summary>
    /// <param name="code">The PacketCode.</param>
    /// <returns>The message associated with the PacketCode.</returns>
    public static string GetMessage(PacketCode code)
    {
        // Try to get the message from the cache
        if (MessageCache.TryGetValue(code, out string? cached))
            return cached;

        // This point should never be reached due to static constructor initialization
        throw new InvalidOperationException($"No message available for {code}.");
    }

    /// <summary>
    /// Gets the message for a PacketCode.
    /// </summary>
    /// <param name="code">The PacketCode.</param>
    /// <returns>The message associated with the PacketCode.</returns>
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
