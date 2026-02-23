// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.SDK.Tools.Models;

/// <summary>
/// Defines how the TCP client should decode incoming packets.
/// </summary>
public enum PacketReceiveDecodeMode
{
    /// <summary>
    /// Do not attempt packet decoding and keep the raw payload only.
    /// </summary>
    RawOnly = 0,

    /// <summary>
    /// Attempt to decode and fall back to raw payload when decoding fails.
    /// </summary>
    BestEffort = 1,

    /// <summary>
    /// Attempt to decode and surface decode failures explicitly.
    /// </summary>
    Strict = 2,
}
