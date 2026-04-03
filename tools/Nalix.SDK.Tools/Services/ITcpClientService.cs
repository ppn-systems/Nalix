using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Networking.Packets;
using Nalix.SDK.Tools.Models;

namespace Nalix.SDK.Tools.Services;

/// <summary>
/// Defines the TCP client service used by the application.
/// </summary>
public interface ITcpClientService : IDisposable
{
    /// <summary>
    /// Gets a value indicating whether the session is connected.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Occurs when the connection state changed.
    /// </summary>
    event EventHandler<string>? StatusChanged;

    /// <summary>
    /// Occurs when a packet was sent.
    /// </summary>
    event EventHandler<PacketLogEntry>? PacketSent;

    /// <summary>
    /// Occurs when a packet was received.
    /// </summary>
    event EventHandler<PacketLogEntry>? PacketReceived;

    /// <summary>
    /// Connects to the specified endpoint.
    /// </summary>
    /// <param name="settings">The target endpoint settings.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task ConnectAsync(ConnectionSettings settings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects the current session.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Serializes and sends the specified packet.
    /// </summary>
    /// <param name="packet">The packet to send.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task SendPacketAsync(IPacket packet, CancellationToken cancellationToken = default);
}
