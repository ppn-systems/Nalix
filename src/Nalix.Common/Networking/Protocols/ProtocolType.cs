// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Common.Networking.Protocols;

/// <summary>
/// Identifies the transport protocol associated with a packet or endpoint.
/// </summary>
public enum ProtocolType : byte
{
    /// <summary>
    /// No transport protocol specified.
    /// </summary>
    NONE = 0x00,

    /// <summary>
    /// Transmission Control Protocol.
    /// </summary>
    TCP = 0x06,

    /// <summary>
    /// User Datagram Protocol.
    /// </summary>
    UDP = 0x11
}
