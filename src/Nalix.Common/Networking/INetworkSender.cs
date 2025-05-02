using Nalix.Common.Package;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nalix.Common.Networking;

/// <summary>
/// Defines a sender for packets and raw bytes over a network stream.
/// </summary>
public interface INetworkSender
{
    /// <summary>
    /// Gets a value indicating whether the network stream is writable.
    /// </summary>
    bool IsStreamHealthy { get; }

    /// <summary>
    /// Sends a packet asynchronously.
    /// </summary>
    /// <param name="packet">The packet to send.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task SendAsync(IPacket packet, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends raw bytes asynchronously.
    /// </summary>
    /// <param name="bytes">The data to send.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task SendAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a packet synchronously.
    /// </summary>
    /// <param name="packet">The packet to send.</param>
    void Send(IPacket packet);

    /// <summary>
    /// Sends raw bytes synchronously.
    /// </summary>
    /// <param name="bytes">The data to send.</param>
    void Send(ReadOnlySpan<byte> bytes);
}
