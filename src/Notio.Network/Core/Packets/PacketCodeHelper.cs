using Notio.Common.Attributes;
using Notio.Common.Package;
using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Notio.Network.Core.Packets;

internal static class PacketCodeHelper
{
    // Thread-safe cache for storing the messages
    private static readonly ConcurrentDictionary<PacketCode, string> MessageCache = new();

    // Static constructor to initialize the cache
    static PacketCodeHelper()
    {
        foreach (PacketCode code in Enum.GetValues<PacketCode>())
        {
            // Access the field directly with DynamicallyAccessedMembers
            FieldInfo? field = GetFieldWithPublicFields(typeof(PacketCode), code.ToString());

            // Get the PacketCodeMessageAttribute from the field
            PacketCodeMessageAttribute? attribute = field?.GetCustomAttribute<PacketCodeMessageAttribute>();

            // Get the message or use a default message
            string message = attribute?.Message ?? $"No message available for {code}.";
            MessageCache[code] = message;
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
        if (MessageCache.TryGetValue(code, out var cached)) return cached;

        // This point should never be reached due to static constructor initialization
        throw new InvalidOperationException($"No message available for {code}.");
    }

    private static FieldInfo? GetFieldWithPublicFields(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)]
        Type type, string fieldName) => type.GetField(fieldName);
}
