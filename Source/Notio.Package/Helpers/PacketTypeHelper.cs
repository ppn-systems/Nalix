using Notio.Package.Enums;
using System;

namespace Notio.Package.Helpers;

public static class PacketTypeHelper
{
    /// <summary>
    /// Determines if the packet type is media-related.
    /// </summary>
    public static bool IsMedia(this PacketType type) =>
        type is PacketType.Image or PacketType.Video or PacketType.Audio;

    /// <summary>
    /// Determines if the packet type is custom or unknown.
    /// </summary>
    public static bool IsCustomOrUnknown(this PacketType type) =>
        type == PacketType.Custom || !Enum.IsDefined(type);

    /// <summary>
    /// Converts the packet type to a user-friendly string.
    /// </summary>
    public static string ToReadableString(this PacketType type) => type switch
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
    public static PacketType Increment(this PacketType type)
    {
        var values = Enum.GetValues<PacketType>();
        int currentIndex = Array.IndexOf(values, type);
        return currentIndex < values.Length - 1 ? values[currentIndex + 1] : type;
    }

    /// <summary>
    /// Safely decrements the PacketType, capping at the lowest defined value.
    /// </summary>
    public static PacketType Decrement(this PacketType type)
    {
        var values = Enum.GetValues<PacketType>();
        int currentIndex = Array.IndexOf(values, type);
        return currentIndex > 0 ? values[currentIndex - 1] : type;
    }

    /// <summary>
    /// Checks if the PacketType is within a specified range.
    /// </summary>
    public static bool IsWithinRange(this PacketType type, PacketType min, PacketType max)
        => type >= min && type <= max;
}