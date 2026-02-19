// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Abstractions;
using Nalix.Common.Networking.Packets;
using Nalix.SDK.Options;

namespace Nalix.SDK.Transport;

/// <summary>
/// A base class for client-side transport sessions (TCP, UDP), 
/// providing a unified API for events and core lifecycle methods.
/// </summary>
public abstract class TransportSession : IDisposable
{
    /// <summary>Gets the transport options used by the session.</summary>
    public abstract TransportOptions Options { get; }

    /// <summary>Gets the packet registry (catalog) used by this session.</summary>
    public abstract IPacketRegistry Catalog { get; }

    /// <summary>Gets a value indicating whether the session is currently connected.</summary>
    public abstract bool IsConnected { get; }

    #region Events

    /// <summary>
    /// Occurs when the session successfully establishes a connection.
    /// </summary>
    public abstract event EventHandler? OnConnected;

    /// <summary>
    /// Occurs when the session is disconnected, providing the exception that caused the disconnect if applicable.
    /// </summary>
    public abstract event EventHandler<Exception>? OnDisconnected;

    /// <summary>
    /// Occurs when a new message (data frame) is received from the remote endpoint.
    /// The receiver takes temporary ownership of the BufferLease and must ensure it is disposed.
    /// </summary>
    public abstract event EventHandler<IBufferLease>? OnMessageReceived;

    /// <summary>
    /// Occurs when a transport-level error is encountered.
    /// </summary>
    public abstract event EventHandler<Exception>? OnError;

    #endregion Events

    #region Methods

    /// <summary>
    /// Asynchronously establishes a connection to the specified remote endpoint.
    /// </summary>
    public abstract Task ConnectAsync(string? host = null, ushort? port = null, CancellationToken ct = default);

    /// <summary>
    /// Asynchronously terminates the current connection.
    /// </summary>
    public abstract Task DisconnectAsync();

    /// <summary>
    /// Asynchronously sends a serialized packet to the remote endpoint.
    /// </summary>
    public abstract Task SendAsync(IPacket packet, CancellationToken ct = default);

    /// <summary>
    /// Asynchronously sends raw binary data to the remote endpoint.
    /// </summary>
    public abstract Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken ct = default);

    /// <summary>
    /// Releases all resources used by the session.
    /// </summary>
    public abstract void Dispose();

    #endregion Methods
}
