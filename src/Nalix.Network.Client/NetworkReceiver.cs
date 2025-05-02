using Nalix.Common.Constants;
using Nalix.Common.Networking;
using Nalix.Common.Package;
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Nalix.Network.Client;

/// <summary>
/// Handles receiving packets from a network stream.
/// </summary>
/// <typeparam name="TPacket">The packet type implementing <see cref="IPacket"/>.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="NetworkReceiver{TPacket}"/> class with the specified network stream.
/// </remarks>
/// <param name="stream">The <see cref="NetworkStream"/> used for receiving data.</param>
/// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
public sealed class NetworkReceiver<TPacket>(NetworkStream stream) : INetworkReceiver<TPacket>
    where TPacket : IPacket, IPacketDeserializer<TPacket>
{
    private readonly NetworkStream _stream = stream ?? throw new ArgumentNullException(nameof(stream));

    /// <summary>
    /// Receives a packet from the network stream.
    /// </summary>
    /// <returns>The deserialized packet implementing <see cref="IPacket"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the stream is not readable or the packet size is invalid.</exception>
    /// <exception cref="EndOfStreamException">Thrown when the stream ends unexpectedly.</exception>
    /// <exception cref="IOException">Thrown when an error occurs while reading from the stream.</exception>
    public TPacket Receive()
    {
        if (!_stream.CanRead)
        {
            throw new InvalidOperationException("The network stream is not readable.");
        }

        // Read 2-byte size header using stackalloc
        Span<byte> header = stackalloc byte[2];
        _stream.ReadExactly(header);

        // Convert to ushort (big-endian)
        ushort length = (ushort)((header[0] << 8) | header[1]);

        if (length < 2)
        {
            throw new InvalidOperationException("Invalid packet size: must be at least 2 bytes.");
        }

        // Use stackalloc for small packets (<= 512 bytes)
        if (length <= PacketConstants.StackAllocLimit)
        {
            Span<byte> sbuffer = stackalloc byte[length];
            _stream.ReadExactly(sbuffer);
            return TPacket.Deserialize(sbuffer);
        }

        // Rent buffer for packet data only
        byte[] buffer = PacketConstants.Pool.Rent(length);
        try
        {
            // Read packet data directly into buffer
            _stream.ReadExactly(buffer, 0, length);

            // Deserialize from buffer
            return TPacket.Deserialize(buffer.AsSpan(0, length));
        }
        finally
        {
            PacketConstants.Pool.Return(buffer);
        }
    }

    /// <summary>
    /// Asynchronously receives a packet from the network stream.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation, containing the deserialized packet.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the stream is not readable or the packet size is invalid.</exception>
    /// <exception cref="EndOfStreamException">Thrown when the stream ends unexpectedly.</exception>
    /// <exception cref="IOException">Thrown when an error occurs while reading from the stream.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled via <paramref name="cancellationToken"/>.</exception>
    public async Task<TPacket> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        if (!_stream.CanRead)
        {
            throw new InvalidOperationException("The network stream is not readable.");
        }

        // Read 2-byte size header using stackalloc
        byte[] header = new byte[2];
        await _stream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);

        // Convert to ushort (big-endian)
        ushort length = (ushort)((header[0] << 8) | header[1]);

        if (length < 2)
        {
            throw new InvalidOperationException("Invalid packet size: must be at least 2 bytes.");
        }

        // Use stackalloc for small packets (<= 512 bytes)
        if (length <= PacketConstants.StackAllocLimit)
        {
            byte[] sbuffer = new byte[length];
            await _stream.ReadExactlyAsync(sbuffer, cancellationToken).ConfigureAwait(false);
            return TPacket.Deserialize(sbuffer);
        }

        // Rent buffer for packet data only
        byte[] buffer = PacketConstants.Pool.Rent(length);
        try
        {
            // Read packet data directly into buffer
            await _stream.ReadExactlyAsync(buffer.AsMemory(0, length), cancellationToken).ConfigureAwait(false);

            // Deserialize from buffer
            return TPacket.Deserialize(buffer.AsSpan(0, length));
        }
        finally
        {
            PacketConstants.Pool.Return(buffer);
        }
    }
}
