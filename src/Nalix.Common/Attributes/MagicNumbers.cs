// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Attributes;

/// <summary>
/// Defines unique magic numbers used to identify specific packet types
/// in the networking and serialization system.  
/// Magic numbers are written in the packet header to quickly determine  
/// how the payload should be interpreted.
/// </summary>
public enum MagicNumbers : System.UInt32
{
    /// <summary>
    /// No magic number assigned.
    /// </summary>
    Unknown = 0x00000000,

    /// <summary>
    /// Represents the handshake process used to establish a connection or agreement between two parties.
    /// </summary>
    Handshake = 0x0000A001,

    /// <summary>
    /// Control packet used for signaling or managing protocol state.
    /// </summary>
    Control = 0x0000A009,

    /// <summary>
    /// Binary data packet with a maximum payload size of 128 bytes.
    /// </summary>
    Binary128 = 0x0000A002,

    /// <summary>
    /// Binary data packet with a maximum payload size of 256 bytes.
    /// </summary>
    Binary256 = 0x0000A003,

    /// <summary>
    /// Binary data packet with a maximum payload size of 512 bytes.
    /// </summary>
    Binary512 = 0x0000A004,

    /// <summary>
    /// Binary data packet with a maximum payload size of 1024 bytes.
    /// </summary>
    Binary1024 = 0x0000A005,

    /// <summary>
    /// UTF-8 text packet with a maximum payload size of 256 bytes.
    /// </summary>
    Text256 = 0x0000A006,

    /// <summary>
    /// UTF-8 text packet with a maximum payload size of 512 bytes.
    /// </summary>
    Text512 = 0x0000A007,

    /// <summary>
    /// UTF-8 text packet with a maximum payload size of 1024 bytes.
    /// </summary>
    Text1024 = 0x0000A008
}
