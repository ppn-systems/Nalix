using Notio.Packets.Enums;
using System;

namespace Notio.Packets.Extensions.Flags;

/// <summary>
/// Provides helper methods for the PacketPriority enum.
/// </summary>
public static class PacketPriorityExtension
{
    /// <summary>
    /// Determines if the priority is urgent.
    /// </summary>
    public static bool IsUrgent(this PacketPriority priority)
        => priority == PacketPriority.Urgent;

    /// <summary>
    /// Determines if the priority is high or above.
    /// </summary>
    public static bool IsHighOrAbove(this PacketPriority priority)
        => priority >= PacketPriority.High;

    /// <summary>
    /// Converts the priority to a user-friendly string.
    /// </summary>
    public static string ToReadableString(this PacketPriority priority) => priority switch
    {
        PacketPriority.Low => "Low Priority",
        PacketPriority.Medium => "Medium Priority",
        PacketPriority.High => "High Priority",
        PacketPriority.Urgent => "Urgent Priority",
        _ => "Unknown Priority"
    };

    /// <summary>
    /// Maps a numeric value to the corresponding PacketPriority, defaulting to Low if invalid.
    /// </summary>
    public static PacketPriority FromValue(byte value)
        => Enum.IsDefined(typeof(PacketPriority), value) ? (PacketPriority)value : PacketPriority.Low;
}