using Notio.Network.Package.Enums;
using System;

namespace Notio.Network.Package.Helpers;

/// <summary>
/// Provides helper methods for the PacketPriority enum.
/// </summary>
public static class PacketPriorityHelper
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
    /// Determines if the priority is low.
    /// </summary>
    public static bool IsLow(this PacketPriority priority)
        => priority == PacketPriority.Low;

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

    /// <summary>
    /// Tries to parse a string into a PacketPriority, defaulting to Low if parsing fails.
    /// </summary>
    public static PacketPriority FromString(string priorityString)
    {
        if (Enum.TryParse(typeof(PacketPriority), priorityString, true, out var result) && result is PacketPriority priority)
        {
            return priority;
        }

        return PacketPriority.Low;
    }

    /// <summary>
    /// Safely increments the priority level, capping at Urgent.
    /// </summary>
    public static PacketPriority Increment(this PacketPriority priority)
        => priority < PacketPriority.Urgent ? priority + 1 : PacketPriority.Urgent;

    /// <summary>
    /// Safely decrements the priority level, capping at Low.
    /// </summary>
    public static PacketPriority Decrement(this PacketPriority priority)
        => priority > PacketPriority.Low ? priority - 1 : PacketPriority.Low;

    /// <summary>
    /// Checks if the priority is within a specific range.
    /// </summary>
    public static bool IsWithinRange(this PacketPriority priority, PacketPriority min, PacketPriority max)
        => priority >= min && priority <= max;
}