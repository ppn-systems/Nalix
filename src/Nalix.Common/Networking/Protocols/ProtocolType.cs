// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Common.Networking.Protocols;

/// <summary>
/// Specifies the transport protocol used by the network packet.
/// </summary>
public enum ProtocolType : byte
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
