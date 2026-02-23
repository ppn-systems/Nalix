// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Messaging.Packets.Abstractions;

/// <summary>
/// Defines a contract for packets that carry time information
/// (e.g., echo, heartbeat, time-sync).
/// </summary>
/// <remarks>
/// Provides both wall-clock time (Unix milliseconds) and
/// monotonic clock ticks (for RTT measurement).
/// </remarks>
public interface IPacketTimestamped : IPacket
{
    /// <summary>
    /// Gets or sets the wall-clock timestamp (Unix epoch milliseconds)
    /// when the packet was created by the sender.
    /// </summary>
    System.Int64 Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the monotonic clock ticks at packet creation.
    /// Useful for RTT measurement, since it is not affected by system clock changes.
    /// </summary>
    System.Int64 MonoTicks { get; set; }
}
