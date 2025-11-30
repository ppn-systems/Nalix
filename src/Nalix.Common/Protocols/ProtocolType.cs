// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Protocols;

/// <summary>
/// Specifies the transport protocol used by the network packet.
/// </summary>
public enum ProtocolType : System.Byte
{
    /// <summary>
    /// No transport protocol specified.
    /// </summary>
    NONE = 0x00,

    /// <summary>
    /// Transmission CONTROL Protocol (reliable, connection-based).
    /// </summary>
    TCP = 0x06,

    /// <summary>
    /// USER Datagram Protocol (unreliable, connectionless).
    /// </summary>
    UDP = 0x11
}
