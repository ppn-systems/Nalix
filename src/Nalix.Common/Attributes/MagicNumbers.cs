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
    Handshake,

    /// <summary>
    /// Binary data packet with a maximum payload size of 128 bytes.
    /// </summary>
    Binary128,

    /// <summary>
    /// Binary data packet with a maximum payload size of 256 bytes.
    /// </summary>
    Binary256,

    /// <summary>
    /// Binary data packet with a maximum payload size of 512 bytes.
    /// </summary>
    Binary512,

    /// <summary>
    /// Binary data packet with a maximum payload size of 1024 bytes.
    /// </summary>
    Binary1024,

    /// <summary>
    /// UTF-8 text packet with a maximum payload size of 256 bytes.
    /// </summary>
    Text256,

    /// <summary>
    /// UTF-8 text packet with a maximum payload size of 512 bytes.
    /// </summary>
    Text512,

    /// <summary>
    /// UTF-8 text packet with a maximum payload size of 1024 bytes.
    /// </summary>
    Text1024
}
