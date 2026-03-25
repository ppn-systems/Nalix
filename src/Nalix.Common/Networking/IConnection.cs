// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Common.Identity;
using Nalix.Common.Security;

namespace Nalix.Common.Networking;

/// <summary>
/// Represents an interface for managing a network connection.
/// </summary>
public partial interface IConnection : IDisposable, IConnectionErrorTracked
{
    /// <summary>
    /// Gets the unique identifier for the connection.
    /// </summary>
    ISnowflake ID { get; }

    /// <summary>
    /// Gets the total duration (in milliseconds) since the connection was established.
    /// Useful for measuring connection lifetime or session activity.
    /// </summary>
    long UpTime { get; }

    /// <summary>
    /// Gets the total number of bytes sent over the connection.
    /// Useful for monitoring bandwidth usage and data transfer statistics.
    /// </summary>
    long BytesSent { get; }

    /// <summary>
    /// Gets the ping time (round-trip time) for the connection, measured in milliseconds.
    /// This value can help determine the latency of the network connection.
    /// </summary>
    long LastPingTime { get; }

    /// <summary>
    /// Key identifying the endpoint associated with the connection.
    /// </summary>
    INetworkEndpoint NetworkEndpoint { get; }

    /// <summary>
    /// Gets the encryption key used for securing communication.
    /// </summary>
    byte[] Secret { get; set; }

    /// <summary>
    /// Gets the authority levels associated with the connection.
    /// </summary>
    PermissionLevel Level { get; set; }

    /// <summary>
    /// Gets or sets the encryption mode used.
    /// </summary>
    CipherSuiteType Algorithm { get; set; }

    /// <summary>
    /// Occurs when the connection is closed.
    /// </summary>
    event EventHandler<IConnectEventArgs> OnCloseEvent;

    /// <summary>
    /// Occurs when data is received and processed.
    /// </summary>
    event EventHandler<IConnectEventArgs> OnProcessEvent;

    /// <summary>
    /// Occurs after data has been successfully processed.
    /// </summary>
    event EventHandler<IConnectEventArgs> OnPostProcessEvent;

    /// <summary>
    /// Closes the connection and releases all associated resources.
    /// </summary>
    /// <param name="force">
    /// <c>true</c> to force the connection closed immediately; otherwise, attempt a normal close.
    /// </param>
    /// <remarks>
    /// Ensures that both the socket and associated streams are properly closed.
    /// </remarks>
    void Close(bool force = false);

    /// <summary>
    /// Disconnects the connection safely with an optional reason.
    /// </summary>
    /// <param name="reason">An optional string providing the reason for disconnection.</param>
    /// <remarks>
    /// Use this method to terminate the connection gracefully.
    /// </remarks>
    void Disconnect(string? reason = null);
}
