using Nalix.Common.Package;
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Nalix.Network.Client;

/// <summary>
/// Handles sending packets and raw bytes over a network stream.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="NetworkSender"/> class with the specified network stream.
/// </remarks>
/// <param name="stream">The <see cref="NetworkStream"/> used for sending data.</param>
/// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
internal class NetworkSender(NetworkStream stream)
{
    private readonly NetworkStream _stream = stream ?? throw new ArgumentNullException(nameof(stream));

    /// <summary>
    /// Checks if the network stream is healthy and writable.
    /// </summary>
    /// <returns><c>true</c> if the stream is writable; otherwise, <c>false</c>.</returns>
    public bool IsStreamHealthy => _stream != null && _stream.CanWrite;

    /// <summary>
    /// Asynchronously sends a packet over the network stream.
    /// </summary>
    /// <param name="packet">The packet to send, implementing <see cref="IPacket"/>.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when the packet is invalid or does not implement <see cref="IPacket"/> correctly.</exception>
    /// <exception cref="IOException">Thrown when an error occurs while writing to the stream.</exception>
    public async Task SendAsync(IPacket packet, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packet);
        await SendAsync(packet.Serialize(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously sends raw bytes over the network stream.
    /// </summary>
    /// <param name="bytes">The bytes to send as a read-only memory segment.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="IOException">Thrown when an error occurs while writing to the stream.</exception>
    public async Task SendAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
    {
        if (!_stream.CanWrite)
            throw new InvalidOperationException("The network stream is not writable.");

        await _stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Synchronously sends a packet over the network stream.
    /// </summary>
    /// <param name="packet">The packet to send, implementing <see cref="IPacket"/>.</param>
    /// <exception cref="ArgumentException">Thrown when the packet is invalid or does not implement <see cref="IPacket"/> correctly.</exception>
    /// <exception cref="IOException">Thrown when an error occurs while writing to the stream.</exception>
    public void Send(IPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        Send(packet.Serialize().Span);
    }

    /// <summary>
    /// Synchronously sends raw bytes over the network stream.
    /// </summary>
    /// <param name="bytes">The bytes to send as a read-only span.</param>
    /// <exception cref="IOException">Thrown when an error occurs while writing to the stream.</exception>
    public void Send(ReadOnlySpan<byte> bytes)
    {
        if (!_stream.CanWrite)
            throw new InvalidOperationException("The network stream is not writable.");

        _stream.Write(bytes);
        _stream.Flush();
    }
}
