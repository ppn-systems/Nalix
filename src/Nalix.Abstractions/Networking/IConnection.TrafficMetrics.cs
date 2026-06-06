// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Abstractions.Networking;

/// <summary>
/// Provides traffic metrics for a network connection.
/// </summary>
public interface IConnectionTrafficMetrics
{
    /// <summary>
    /// Gets the total number of bytes sent over the connection.
    /// Useful for monitoring bandwidth usage and data transfer statistics.
    /// </summary>
    long BytesSent { get; }

    /// <summary>
    /// Gets the total number of bytes received over the life of the connection.
    /// </summary>
    long BytesReceived { get; }

    /// <summary>
    /// Gets the total number of dropped packets for this connection.
    /// </summary>
    long PacketsDropped { get; }

    /// <summary>
    /// Increments the total number of bytes sent.
    /// </summary>
    void IncrementBytesSent(int bytes);

    /// <summary>
    /// Increments the total number of bytes received.
    /// </summary>
    void IncrementBytesReceived(int bytes);

    /// <summary>
    /// Increments the total number of dropped packets.
    /// </summary>
    void IncrementPacketsDropped();
}
