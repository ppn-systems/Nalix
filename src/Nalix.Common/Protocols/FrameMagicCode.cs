// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Protocols;

/// <summary>
/// Defines reserved <c>MagicNumber</c> values used internally by the framework
/// for core packet types (handshake, control, text, and binary).
/// <para>
/// <b>Important:</b> The range <c>0x0000_A000</c> – <c>0x0000_AFFF</c> is
/// reserved for system/default packets.
/// Do not assign custom packets within this range to avoid collisions.
/// </para>
/// <para>
/// Custom packet types should use a different range,
/// e.g., <c>0x0100_0000</c> and above for application-level protocols.
/// </para>
/// </summary>
public enum FrameMagicCode : System.UInt32
{
    /// <summary>
    /// No magic number assigned.
    /// </summary>
    NONE = 0x0000_A000,

    /// <summary>
    /// Represents the handshake process used to establish a connection or
    /// agreement between two parties.
    /// </summary>
    HANDSHAKE = 0x0000_A001,

    /// <summary>
    /// CONTROL packet used for signaling or managing protocol state.
    /// </summary>
    CONTROL = 0x0000_A002,

    /// <summary>
    /// Binary data packet with a maximum payload size of 128 bytes.
    /// </summary>
    BINARY128 = 0x0000_A003,

    /// <summary>
    /// Binary data packet with a maximum payload size of 256 bytes.
    /// </summary>
    BINARY256 = 0x0000_A004,

    /// <summary>
    /// Binary data packet with a maximum payload size of 512 bytes.
    /// </summary>
    BINARY512 = 0x0000_A005,

    /// <summary>
    /// Binary data packet with a maximum payload size of 1024 bytes.
    /// </summary>
    BINARY1024 = 0x0000_A006,

    /// <summary>
    /// UTF-8 text packet with a maximum payload size of 256 bytes.
    /// </summary>
    TEXT256 = 0x0000_A007,

    /// <summary>
    /// UTF-8 text packet with a maximum payload size of 512 bytes.
    /// </summary>
    TEXT512 = 0x0000_A008,

    /// <summary>
    /// UTF-8 text packet with a maximum payload size of 1024 bytes.
    /// </summary>
    TEXT1024 = 0x0000_A009,

    /// <summary>
    /// DIRECTIVE packet used for sending commands or instructions.
    /// </summary>
    DIRECTIVE = 0x0000_A00A,

    /// <summary>
    /// Time synchronization packet used for aligning clocks between systems.
    /// </summary>
    TIME_SYNC = 0x0000_A00B,
}
