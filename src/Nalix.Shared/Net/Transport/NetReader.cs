using Nalix.Common.Constants;
using Nalix.Common.Networking;
using Nalix.Common.Package;

namespace Nalix.Shared.Net.Transport;

/// <summary>
/// Handles receiving packets from a network stream.
/// </summary>
/// <typeparam name="TPacket">The packet type implementing <see cref="IPacket"/>.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="NetReader{TPacket}"/> class with the specified network stream.
/// </remarks>
/// <param name="stream">The <see cref="System.Net.Sockets.NetworkStream"/> used for receiving data.</param>
/// <exception cref="System.ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
public sealed class NetReader<TPacket>(System.Net.Sockets.NetworkStream stream) : INetworkReceiver<TPacket>
    where TPacket : IPacket, IPacketDeserializer<TPacket>
{
    private readonly System.Net.Sockets.NetworkStream _stream = stream
        ?? throw new System.ArgumentNullException(nameof(stream));

    /// <summary>
    /// Receives a packet from the network stream.
    /// </summary>
    /// <returns>The deserialized packet implementing <see cref="IPacket"/>.</returns>
    /// <exception cref="System.InvalidOperationException">Thrown when the stream is not readable or the packet size is invalid.</exception>
    /// <exception cref="System.IO.EndOfStreamException">Thrown when the stream ends unexpectedly.</exception>
    /// <exception cref="System.IO.IOException">Thrown when an error occurs while reading from the stream.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public TPacket Receive()
    {
        if (!_stream.CanRead)
        {
            throw new System.InvalidOperationException("The network stream is not readable.");
        }

        // Read 2-byte size header using stackalloc
        System.Span<byte> header = stackalloc byte[2];
        _stream.ReadExactly(header);

        // Convert to ushort (big-endian)
        ushort length = (ushort)((header[0] << 8) | header[1]);

        if (length < 2)
        {
            throw new System.InvalidOperationException("Invalid packet size: must be at least 2 bytes.");
        }

        // Use stackalloc for small packets (<= 512 bytes)
        if (length <= PacketConstants.StackAllocLimit)
        {
            System.Span<byte> sbuffer = stackalloc byte[length];
            sbuffer[0] = header[0];
            sbuffer[1] = header[1];
            _stream.ReadExactly(sbuffer[2..]);
            return TPacket.Deserialize(sbuffer);
        }

        // Rent buffer for packet data only
        byte[] buffer = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(length);
        try
        {
            buffer[0] = header[0];
            buffer[1] = header[1];

            // Read packet data directly into buffer
            _stream.ReadExactly(buffer, 2, length - 2);

            // Deserialize from buffer
            return TPacket.Deserialize(System.MemoryExtensions.AsSpan(buffer, 0, length));
        }
        finally
        {
            System.Buffers.ArrayPool<System.Byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Asynchronously receives a packet from the network stream.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation, containing the deserialized packet.</returns>
    /// <exception cref="System.InvalidOperationException">Thrown when the stream is not readable or the packet size is invalid.</exception>
    /// <exception cref="System.IO.EndOfStreamException">Thrown when the stream ends unexpectedly.</exception>
    /// <exception cref="System.IO.IOException">Thrown when an error occurs while reading from the stream.</exception>
    /// <exception cref="System.OperationCanceledException">Thrown when the operation is canceled via <paramref name="cancellationToken"/>.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public async System.Threading.Tasks.Task<TPacket> ReceiveAsync(
        System.Threading.CancellationToken cancellationToken = default)
    {
        if (!_stream.CanRead)
        {
            throw new System.InvalidOperationException("The network stream is not readable.");
        }

        // Read 2-byte size header using stackalloc
        byte[] header = new byte[2];
        await _stream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);

        // Convert to ushort (big-endian)
        ushort length = (ushort)((header[0] << 8) | header[1]);

        if (length < 2)
        {
            throw new System.InvalidOperationException("Invalid packet size: must be at least 2 bytes.");
        }

        // Use stackalloc for small packets (<= 512 bytes)
        if (length <= PacketConstants.StackAllocLimit)
        {
            byte[] sbuffer = new byte[length];
            System.Array.Copy(header, sbuffer, 2);
            await _stream.ReadExactlyAsync(
                System.MemoryExtensions.AsMemory(sbuffer, 2, length - 2),
                cancellationToken).ConfigureAwait(false);

            return TPacket.Deserialize(sbuffer);
        }

        // Rent buffer for packet data only
        byte[] buffer = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(length);
        try
        {
            // Read packet data directly into buffer
            await _stream.ReadExactlyAsync(
                System.MemoryExtensions.AsMemory(buffer, 0, length),
                cancellationToken).ConfigureAwait(false);

            // Deserialize from buffer
            return TPacket.Deserialize(System.MemoryExtensions.AsSpan(buffer, 0, length));
        }
        finally
        {
            System.Buffers.ArrayPool<System.Byte>.Shared.Return(buffer);
        }
    }
}
