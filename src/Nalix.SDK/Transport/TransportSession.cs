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
/// Provides the common contract for client-side transport sessions.
/// </summary>
/// <remarks>
/// Derived sessions expose a consistent lifecycle for connecting, disconnecting,
/// sending packets, and receiving transport events.
/// </remarks>
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
    /// Occurs when the session establishes a connection.
    /// </summary>
    public abstract event EventHandler? OnConnected;

    /// <summary>
    /// Occurs when the session disconnects.
    /// </summary>
    public abstract event EventHandler<Exception>? OnDisconnected;

    /// <summary>
    /// Occurs when a message is received from the remote endpoint.
    /// The handler receives temporary ownership of the buffer lease and must dispose it.
    /// </summary>
    public abstract event EventHandler<IBufferLease>? OnMessageReceived;

    /// <summary>
    /// Occurs when a transport-level error is encountered.
    /// </summary>
    public abstract event EventHandler<Exception>? OnError;

    #endregion Events

    #region Methods

    /// <summary>
    /// Asynchronously connects to the configured remote endpoint.
    /// </summary>
    /// <param name="host">The target host name or address. If <see langword="null"/> or empty, the configured default is used.</param>
    /// <param name="port">The target port. If <see langword="null"/>, the configured default is used.</param>
    /// <param name="ct">The token to observe while connecting.</param>
    public abstract Task ConnectAsync(string? host = null, ushort? port = null, CancellationToken ct = default);

    /// <summary>
    /// Asynchronously disconnects from the remote endpoint.
    /// </summary>
    public abstract Task DisconnectAsync();

    /// <summary>
    /// Asynchronously sends a packet to the remote endpoint.
    /// </summary>
    /// <param name="packet">The packet to send.</param>
    /// <param name="ct">The token to observe while sending.</param>
    public abstract Task SendAsync(IPacket packet, CancellationToken ct = default);

    /// <summary>
    /// Asynchronously sends raw binary data to the remote endpoint.
    /// </summary>
    /// <param name="payload">The payload to send.</param>
    /// <param name="ct">The token to observe while sending.</param>
    public abstract Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken ct = default);

    /// <summary>
    /// Releases all resources used by the session.
    /// </summary>
    public abstract void Dispose();

    #endregion Methods
}
