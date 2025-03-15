using Notio.Common.Package;
using System;

namespace Notio.Network.Package.Extensions;

/// <summary>
/// Provides extensions methods for the PacketPriority enum.
/// </summary>
public static class PacketPriorityExtensions
{
    /// <summary>
    /// Determines if the priority is within the valid range of priorities.
    /// </summary>
    public static bool IsValid(this PacketPriority priority)
        => Enum.IsDefined(priority);

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
    /// Checks if the priority is within a specific range.
    /// </summary>
    public static bool IsWithinRange(this PacketPriority priority, PacketPriority min, PacketPriority max)
        => priority >= min && priority <= max;
}
