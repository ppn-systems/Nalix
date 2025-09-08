// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Abstractions;
using Nalix.Common.Caching;
using Nalix.Common.Security.Enums;
using Nalix.Common.Security.Types;

namespace Nalix.Common.Connection;

/// <summary>
/// Represents an interface for managing a network connection.
/// </summary>
public partial interface IConnection : System.IDisposable
{
    /// <summary>
    /// Gets the unique identifier for the connection.
    /// </summary>
    IIdentifier ID { get; }

    /// <summary>
    /// Gets the total duration (in milliseconds) since the connection was established.
    /// Useful for measuring connection lifetime or session activity.
    /// </summary>
    System.Int64 UpTime { get; }

    /// <summary>
    /// Gets the ping time (round-trip time) for the connection, measured in milliseconds.
    /// This value can help determine the latency of the network connection.
    /// </summary>
    System.Int64 LastPingTime { get; }

    /// <summary>
    /// Gets the incoming packet of data.
    /// </summary>
    IBufferLease IncomingPacket { get; }

    /// <summary>
    /// Gets the remote endpoint address associated with the connection.
    /// </summary>
    System.Net.EndPoint RemoteEndPoint { get; }

    /// <summary>
    /// Gets the encryption key used for securing communication.
    /// </summary>
    System.Byte[] EncryptionKey { get; set; }

    /// <summary>
    /// Gets the authority levels associated with the connection.
    /// </summary>
    PermissionLevel Level { get; set; }

    /// <summary>
    /// Gets or sets the encryption mode used.
    /// </summary>
    SymmetricAlgorithmType Encryption { get; set; }

    /// <summary>
    /// Occurs when the connection is closed.
    /// </summary>
    event System.EventHandler<IConnectEventArgs> OnCloseEvent;

    /// <summary>
    /// Occurs when data is received and processed.
    /// </summary>
    event System.EventHandler<IConnectEventArgs> OnProcessEvent;

    /// <summary>
    /// Occurs after data has been successfully processed.
    /// </summary>
    event System.EventHandler<IConnectEventArgs> OnPostProcessEvent;

    /// <summary>
    /// Closes the connection and releases all associated resources.
    /// </summary>
    /// <remarks>
    /// Ensures that both the socket and associated streams are properly closed.
    /// </remarks>
    void Close(System.Boolean force = false);

    /// <summary>
    /// Disconnects the connection safely with an optional reason.
    /// </summary>
    /// <param name="reason">An optional string providing the reason for disconnection.</param>
    /// <remarks>
    /// Use this method to terminate the connection gracefully.
    /// </remarks>
    void Disconnect(System.String reason = null);
}
