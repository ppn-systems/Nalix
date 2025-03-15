using Notio.Common.Package;
using System;

namespace Notio.Network.Package.Extensions;

/// <summary>
/// Provides extension methods for packet types.
/// </summary>
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
}
