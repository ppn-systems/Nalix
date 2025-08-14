// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Packets.Enums;

/// <summary>
/// Specifies the relative priority level of a network packet.
/// </summary>
/// <remarks>
/// The priority level may influence the order in which packets are processed or transmitted.
/// Higher values generally indicate a greater urgency for delivery.
/// </remarks>
public enum PacketPriority : System.Byte
{
    /// <summary>
    /// Standard priority level for most packets.
    /// No special handling is required.
    /// </summary>
    Normal = 0x00,

    /// <summary>
    /// Lower-than-normal priority.
    /// May be delayed in favor of higher-priority packets.
    /// </summary>
    Low = 0x01,

    /// <summary>
    /// Moderate priority level, between <see cref="Low"/> and <see cref="High"/>.
    /// Useful for packets that should be delivered sooner than normal but are not urgent.
    /// </summary>
    Medium = 0x02,

    /// <summary>
    /// Higher-than-normal priority.
    /// These packets should be processed or transmitted before <see cref="Normal"/> and <see cref="Medium"/> packets.
    /// </summary>
    High = 0x03,

    /// <summary>
    /// Highest priority level.
    /// Intended for urgent packets that must be processed or delivered as soon as possible.
    /// </summary>
    Urgent = 0x04
}
