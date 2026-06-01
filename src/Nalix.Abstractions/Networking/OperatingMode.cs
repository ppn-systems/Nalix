// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Abstractions.Networking;

/// <summary>
/// Defines how UDP traffic is handled.
/// </summary>
public enum OperatingMode : byte
{
    /// <summary>
    /// Session-aware mode that supports connection tracking,
    /// authentication, sequence validation, and protocol processing.
    /// </summary>
    Server,

    /// <summary>
    /// Raw forwarding mode that bypasses session management,
    /// authentication, and protocol-level processing.
    /// Datagram payloads are forwarded directly.
    /// </summary>
    Passthrough
}
