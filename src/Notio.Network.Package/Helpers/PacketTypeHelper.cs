using Notio.Common.Package;
using System;

namespace Notio.Network.Package.Helpers;

/// <summary>
/// Provides helper methods for packet types.
/// </summary>
public static class PacketTypeHelper
{
    /// <summary>
    /// Converts the packet type to a user-friendly string.
    /// </summary>
    public static string ToReadableString(PacketType type) => type switch
    {
        PacketType.None => "No Payload",
        PacketType.Int => "Integer",
        PacketType.Long => "Long Integer",
        PacketType.String => "String",
        PacketType.Json => "JSON Data",
        PacketType.Xaml => "XAML Data",
        PacketType.Xml => "XML Data",
        PacketType.Binary => "Binary Data",
        PacketType.File => "File Data",
        PacketType.Image => "Image",
        PacketType.Video => "Video",
        PacketType.Audio => "Audio",
        PacketType.Custom => "Custom Payload",
        _ => "Unknown Payload"
    };

    /// <summary>
    /// Maps a string value to the corresponding PacketType, defaulting to None if invalid.
    /// </summary>
    public static PacketType FromString(string typeString)
    {
        if (Enum.TryParse(typeof(PacketType), typeString, true, out var result) && result is PacketType type)
        {
            return type;
        }

        return PacketType.None;
    }

    /// <summary>
    /// Safely increments the PacketType, capping at the highest defined value.
    /// </summary>
    public static PacketType Increment(PacketType type)
    {
        var values = Enum.GetValues<PacketType>();
        int currentIndex = Array.IndexOf(values, type);
        return currentIndex < values.Length - 1 ? values[currentIndex + 1] : type;
    }

    /// <summary>
    /// Safely decrements the PacketType, capping at the lowest defined value.
    /// </summary>
    public static PacketType Decrement(PacketType type)
    {
        var values = Enum.GetValues<PacketType>();
        int currentIndex = Array.IndexOf(values, type);
        return currentIndex > 0 ? values[currentIndex - 1] : type;
    }
}
