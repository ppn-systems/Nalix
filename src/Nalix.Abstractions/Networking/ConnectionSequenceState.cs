// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Abstractions.Networking;

/// <summary>
/// Maintains sequence numbers for connection transports.
/// Pooled to avoid allocations.
/// </summary>
public sealed class ConnectionSequenceState
{
    /// <summary>
    /// Stores the last TCP send sequence number (for session resume).
    /// </summary>
    public uint TcpSendSequence { get; set; }

    /// <summary>
    /// Stores the last TCP receive sequence number (for session resume).
    /// </summary>
    public uint TcpReceiveSequence { get; set; }

    /// <summary>
    /// Stores the last UDP send sequence number (for session resume).
    /// </summary>
    public uint UdpSendSequence { get; set; }

    /// <summary>
    /// Stores the last UDP receive sequence number (for session resume).
    /// </summary>
    public uint UdpReceiveSequence { get; set; }

    /// <inheritdoc/>
    public void ResetForPool()
    {
        this.TcpSendSequence = 0;
        this.TcpReceiveSequence = 0;
        this.UdpSendSequence = 0;
        this.UdpReceiveSequence = 0;
    }
}
