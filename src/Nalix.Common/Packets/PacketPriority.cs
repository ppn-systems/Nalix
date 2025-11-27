// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Packets;

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
    NONE = 0x00,

    /// <summary>
    /// Lower-than-normal priority.
    /// May be delayed in favor of higher-priority packets.
    /// </summary>
    LOW = 0x01,

    /// <summary>
    /// Moderate priority level, between <see cref="LOW"/> and <see cref="HIGH"/>.
    /// Useful for packets that should be delivered sooner than normal but are not urgent.
    /// </summary>
    MEDIUM = 0x02,

    /// <summary>
    /// Higher-than-normal priority.
    /// These packets should be processed or transmitted before <see cref="NONE"/> and <see cref="MEDIUM"/> packets.
    /// </summary>
    HIGH = 0x03,

    /// <summary>
    /// Highest priority level.
    /// Intended for urgent packets that must be processed or delivered as soon as possible.
    /// </summary>
    URGENT = 0x04
}
