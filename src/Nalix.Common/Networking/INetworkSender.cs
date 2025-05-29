using Nalix.Common.Package;

namespace Nalix.Common.Networking;

/// <summary>
/// Defines a sender for packets and raw bytes over a network stream.
/// </summary>
public interface INetworkSender<TPacket> where TPacket : IPacket
{
    /// <summary>
    /// Gets a value indicating whether the network stream is writable.
    /// </summary>
    System.Boolean IsStreamHealthy { get; }

    /// <summary>
    /// Sends a packet synchronously.
    /// </summary>
    /// <param name="packet">The packet to send.</param>
    void Send(TPacket packet);

    /// <summary>
    /// Sends raw bytes synchronously.
    /// </summary>
    /// <param name="bytes">The data to send.</param>
    void Send(System.ReadOnlySpan<System.Byte> bytes);

    /// <summary>
    /// Sends a packet asynchronously.
    /// </summary>
    /// <param name="packet">The packet to send.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    System.Threading.Tasks.Task SendAsync(
        TPacket packet,
        System.Threading.CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends raw bytes asynchronously.
    /// </summary>
    /// <param name="bytes">The data to send.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    System.Threading.Tasks.Task SendAsync(
        System.ReadOnlyMemory<System.Byte> bytes,
        System.Threading.CancellationToken cancellationToken = default);
}
