using Notio.Packets.Enums;
using System;

namespace Notio.Packets.Extensions;

public static class PacketTypeExtensions
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
}
