using Notio.Common.Package;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Common.Connection;

public partial interface IConnection
{
    /// <summary>
    /// Starts receiving data from the connection.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token to cancel the receiving operation.
    /// </param>
    /// <remarks>
    /// Call this method to initiate listening for incoming data on the connection.
    /// </remarks>
    void BeginReceive(CancellationToken cancellationToken = default);

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
    bool Send(ReadOnlySpan<byte> message);

    /// <summary>
    /// Sends a message asynchronously over the connection.
    /// </summary>
    /// <param name="packet">The packet to send.</param>
    /// <param name="cancellationToken">A token to cancel the sending operation.</param>
    /// <returns>A task that represents the asynchronous sending operation.</returns>
    /// <remarks>
    /// If the connection has been authenticated, the data will be encrypted before sending.
    /// </remarks>
    Task<bool> SendAsync(IPacket packet, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a message asynchronously over the connection.
    /// </summary>
    /// <param name="message">The data to send.</param>
    /// <param name="cancellationToken">A token to cancel the sending operation.</param>
    /// <returns>A task that represents the asynchronous sending operation.</returns>
    /// <remarks>
    /// If the connection has been authenticated, the data will be encrypted before sending.
    /// </remarks>
    Task<bool> SendAsync(ReadOnlyMemory<byte> message, CancellationToken cancellationToken = default);
}
