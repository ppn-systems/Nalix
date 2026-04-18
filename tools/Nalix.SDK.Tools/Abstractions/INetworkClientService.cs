// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Networking.Packets;
using Nalix.SDK.Tools.Models;

namespace Nalix.SDK.Tools.Abstractions;

/// <summary>
/// Defines network client operations used by the packet tool.
/// </summary>
public interface INetworkClientService : IDisposable
{
    /// <summary>
    /// Gets a value indicating whether the client is connected.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Gets the current transport protocol.
    /// </summary>
    PacketFlags Transport { get; }

    /// <summary>
    /// Gets the session token (Snowflake) if available.
    /// </summary>
    Nalix.Framework.Identifiers.Snowflake SessionToken { get; }

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
    /// Connects the network session (TCP or UDP).
    /// </summary>
    /// <param name="settings">The connection settings.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task ConnectAsync(ConnectionSettings settings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects the active session.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a packet through the active session.
    /// </summary>
    /// <param name="packet">The packet to send.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task SendPacketAsync(IPacket packet, bool? encrypt = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Performs the cryptographic handshake to establish a secure session.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task HandshakeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to resume an existing session state.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task ResumeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a ping to the server to measure round-trip time.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The measured RTT in milliseconds.</returns>
    Task<double> PingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets or sets a value indicating whether auto-ping is enabled.
    /// </summary>
    bool AutoPingEnabled { get; set; }
}
