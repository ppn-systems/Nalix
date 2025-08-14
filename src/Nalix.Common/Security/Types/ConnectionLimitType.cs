// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Security.Types;

/// <summary>
/// Defines preset connection limit levels for controlling the number of simultaneous connections.
/// </summary>
public enum ConnectionLimitType : System.Byte
{
    /// <summary>
    /// Low connection limit, typically for minimal traffic environments.
    /// </summary>
    Low = 0x1A,

    /// <summary>
    /// Medium connection limit, suitable for moderate traffic environments.
    /// </summary>
    Medium = 0x3C,

    /// <summary>
    /// High connection limit, suitable for high-traffic environments.
    /// </summary>
    High = 0x5E,

    /// <summary>
    /// Unlimited simultaneous connections, with no restriction on the number of connections.
    /// </summary>
    Unlimited = 0x7F
}
