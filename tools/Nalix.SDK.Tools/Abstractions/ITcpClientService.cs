// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Networking.Packets;
using Nalix.SDK.Tools.Models;

namespace Nalix.SDK.Tools.Abstractions;

/// <summary>
/// Defines TCP client operations used by the packet tool.
/// </summary>
public interface ITcpClientService : IDisposable
{
    /// <summary>
    /// Gets a value indicating whether the TCP client is connected.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Raised when the status text changes.
    /// </summary>
    event EventHandler<string>? StatusChanged;

    /// <summary>
    /// Raised when a packet has been sent.
    /// </summary>
    event EventHandler<PacketLogEntry>? PacketSent;

    /// <summary>
    /// Raised when a packet has been received.
    /// </summary>
    event EventHandler<PacketLogEntry>? PacketReceived;

    /// <summary>
    /// Connects the TCP session.
    /// </summary>
    /// <param name="settings">The connection settings.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task ConnectAsync(ConnectionSettings settings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects the TCP session.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a packet through the active TCP session.
    /// </summary>
    /// <param name="packet">The packet to send.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task SendPacketAsync(IPacket packet, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Performs the cryptographic handshake to establish a secure session.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task HandshakeAsync(CancellationToken cancellationToken = default);
}
