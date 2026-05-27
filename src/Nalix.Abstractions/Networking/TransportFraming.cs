// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Abstractions.Networking;

/// <summary>
/// Defines the framing mechanism used at the transport layer to delineate packet boundaries.
/// </summary>
public enum TransportFraming : byte
{
    /// <summary>
    /// No framing is applied. Raw bytes are sent and received.
    /// </summary>
    None = 0,

    /// <summary>
    /// The default Nalix framing.
    /// Uses a 2-byte little-endian length prefix.
    /// </summary>
    UInt16LengthPrefixed = 1,

    /// <summary>
    /// Minecraft-style LEB128 Variable-Length Integer (VarInt) prefix.
    /// Highly compressed for small packets.
    /// </summary>
    VarIntLengthPrefixed = 2
}
