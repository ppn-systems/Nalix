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

    /// <summary>Occurs when the session successfully connects.</summary>
    public abstract event EventHandler? OnConnected;

    /// <summary>Occurs on disconnect. Argument is the reason or <c>null</c>.</summary>
    public abstract event EventHandler<Exception>? OnDisconnected;

    /// <summary>Occurs when a raw frame is received and decoded.</summary>
    public abstract event EventHandler<IBufferLease>? OnMessageReceived;

    /// <summary>Occurs when an internal transport error happens.</summary>
    public abstract event EventHandler<Exception>? OnError;

    #endregion Events

    #region Methods

    /// <summary>Connects asynchronously to the remote endpoint.</summary>
    public abstract Task ConnectAsync(string? host = null, ushort? port = null, CancellationToken ct = default);

    /// <summary>Disconnects and stops all background processes.</summary>
    public abstract Task DisconnectAsync();

    /// <summary>Serializes and sends a packet asynchronously.</summary>
    public abstract Task SendAsync(IPacket packet, CancellationToken ct = default);

    /// <summary>Sends raw payload bytes asynchronously.</summary>
    public abstract Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken ct = default);

    /// <inheritdoc/>
    public abstract void Dispose();

    #endregion Methods
}
