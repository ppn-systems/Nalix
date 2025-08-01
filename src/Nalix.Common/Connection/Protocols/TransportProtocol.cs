﻿namespace Nalix.Common.Connection.Protocols;

/// <summary>
/// Specifies the transport protocol used by the network packet.
/// </summary>
public enum TransportProtocol : System.Byte
{
    /// <summary>
    /// Transmission Control Protocol (reliable, connection-based).
    /// </summary>
    Tcp = 0x3A,

    /// <summary>
    /// User Datagram Protocol (unreliable, connectionless).
    /// </summary>
    Udp = 0xE1
}
