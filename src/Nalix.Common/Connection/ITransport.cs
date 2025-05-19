using Nalix.Common.Package;

namespace Nalix.Common.Connection;

/// <summary>
/// Represents a transport interface for sending data packets.
/// </summary>
public interface ITransport
{
    /// <summary>
    /// Sends a packet synchronously over the connection.
    /// </summary>
    /// <param name="packet">The packet to send.</param>
    /// <returns></returns>
    bool Send(IPacket packet);

    /// <summary>
    /// Sends a message synchronously over the connection.
    /// </summary>
    /// <param name="message">The message to send.</param>
    bool Send(System.ReadOnlySpan<byte> message);

    /// <summary>
    /// Sends a message asynchronously over the connection.
    /// </summary>
    /// <param name="packet">The packet to send.</param>
    /// <param name="cancellationToken">A token to cancel the sending operation.</param>
    /// <returns>A task that represents the asynchronous sending operation.</returns>
    /// <remarks>
    /// If the connection has been authenticated, the data will be encrypted before sending.
    /// </remarks>
    System.Threading.Tasks.Task<bool> SendAsync(
        IPacket packet,
        System.Threading.CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a message asynchronously over the connection.
    /// </summary>
    /// <param name="message">The data to send.</param>
    /// <param name="cancellationToken">A token to cancel the sending operation.</param>
    /// <returns>A task that represents the asynchronous sending operation.</returns>
    /// <remarks>
    /// If the connection has been authenticated, the data will be encrypted before sending.
    /// </remarks>
    System.Threading.Tasks.Task<bool> SendAsync(
        System.ReadOnlyMemory<byte> message,
        System.Threading.CancellationToken cancellationToken = default);
}
