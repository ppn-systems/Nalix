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
public enum FrameMagic : System.UInt32
{
    /// <summary>
    /// No magic number assigned.
    /// </summary>
    Unknown = 0x0000_A000,

    /// <summary>
    /// Represents the handshake process used to establish a connection or
    /// agreement between two parties.
    /// </summary>
    Handshake = 0x0000_A001,

    /// <summary>
    /// Control packet used for signaling or managing protocol state.
    /// </summary>
    Control = 0x0000_A002,

    /// <summary>
    /// Binary data packet with a maximum payload size of 128 bytes.
    /// </summary>
    Binary128 = 0x0000_A003,

    /// <summary>
    /// Binary data packet with a maximum payload size of 256 bytes.
    /// </summary>
    Binary256 = 0x0000_A004,

    /// <summary>
    /// Binary data packet with a maximum payload size of 512 bytes.
    /// </summary>
    Binary512 = 0x0000_A005,

    /// <summary>
    /// Binary data packet with a maximum payload size of 1024 bytes.
    /// </summary>
    Binary1024 = 0x0000_A006,

    /// <summary>
    /// UTF-8 text packet with a maximum payload size of 256 bytes.
    /// </summary>
    Text256 = 0x0000_A007,

    /// <summary>
    /// UTF-8 text packet with a maximum payload size of 512 bytes.
    /// </summary>
    Text512 = 0x0000_A008,

    /// <summary>
    /// UTF-8 text packet with a maximum payload size of 1024 bytes.
    /// </summary>
    Text1024 = 0x0000_A009,

    /// <summary>
    /// Directive packet used for sending commands or instructions.
    /// </summary>
    Directive = 0x0000_A00A
}
