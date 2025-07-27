using Nalix.Common.Packets;

namespace Nalix.SDK.Remote.Internal;

/// <summary>
/// Handles receiving packets from a network stream with unsafe optimizations.
/// </summary>
/// <typeparam name="TPacket">The packet type implementing <see cref="IPacket"/>.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="RemoteStreamReceiver{TPacket}"/> class with the specified network stream.
/// </remarks>
/// <param name="stream">The <see cref="System.Net.Sockets.NetworkStream"/> used for receiving data.</param>
/// <exception cref="System.ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
[System.ComponentModel.EditorBrowsable(
    System.ComponentModel.EditorBrowsableState.Never)]
internal sealed class RemoteStreamReceiver<TPacket>(System.Net.Sockets.NetworkStream stream)
    where TPacket : IPacket, IPacketTransformer<TPacket>
{
    private readonly System.Net.Sockets.NetworkStream _stream = stream
        ?? throw new System.ArgumentNullException(nameof(stream));

    /// <summary>
    /// Receives a packet from the network stream using unsafe optimizations.
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
        System.Span<System.Byte> header = stackalloc System.Byte[2];
        _stream.ReadExactly(header);

        // Unsafe fast conversion to ushort (big-endian)
        System.UInt16 length;
        unsafe
        {
            fixed (System.Byte* headerPtr = header)
            {
                // Direct memory access for big-endian conversion
                length = (System.UInt16)((headerPtr[0] << 8) | headerPtr[1]);
            }
        }

        if (length < 2)
        {
            throw new System.InvalidOperationException("Invalid packet size: must be at least 2 bytes.");
        }

        // Use stackalloc for small packets (<= 512 bytes) with unsafe optimization
        if (length <= PacketConstants.StackAllocLimit)
        {
            System.Span<System.Byte> sbuffer = stackalloc System.Byte[length];

            // Unsafe fast copy of header
            unsafe
            {
                fixed (System.Byte* bufferPtr = sbuffer)
                fixed (System.Byte* headerPtr = header)
                {
                    // Direct memory copy - fastest possible
                    System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(
                        bufferPtr, headerPtr, 2);
                }
            }

            _stream.ReadExactly(sbuffer[2..]);
            return TPacket.Deserialize(sbuffer);
        }

        // Rent buffer for larger packets
        System.Byte[] buffer = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(length);
        try
        {
            // Unsafe fast copy of header to rented buffer
            unsafe
            {
                fixed (System.Byte* bufferPtr = buffer)
                fixed (System.Byte* headerPtr = header)
                {
                    System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(
                        bufferPtr, headerPtr, 2);
                }
            }

            // Read remaining packet data
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
    /// Asynchronously receives a packet from the network stream with unsafe optimizations.
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

        // Read 2-byte size header
        System.Byte[] header = new System.Byte[2];
        await _stream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);

        // Unsafe fast conversion to ushort (big-endian)
        System.UInt16 length;
        unsafe
        {
            fixed (System.Byte* headerPtr = header)
            {
                length = (System.UInt16)((headerPtr[0] << 8) | headerPtr[1]);
            }
        }

        if (length < 2)
        {
            throw new System.InvalidOperationException("Invalid packet size: must be at least 2 bytes.");
        }

        // For small packets, use array with unsafe optimization
        if (length <= PacketConstants.StackAllocLimit)
        {
            System.Byte[] sbuffer = new System.Byte[length];

            // Unsafe fast copy of header
            unsafe
            {
                fixed (System.Byte* bufferPtr = sbuffer)
                fixed (System.Byte* headerPtr = header)
                {
                    System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(
                        bufferPtr, headerPtr, 2);
                }
            }

            await _stream.ReadExactlyAsync(
                System.MemoryExtensions.AsMemory(sbuffer, 2, length - 2),
                cancellationToken).ConfigureAwait(false);

            return TPacket.Deserialize(sbuffer);
        }

        // Rent buffer for larger packets
        System.Byte[] buffer = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(length);
        try
        {
            // Unsafe fast copy of header to rented buffer
            unsafe
            {
                fixed (System.Byte* bufferPtr = buffer)
                fixed (System.Byte* headerPtr = header)
                {
                    System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(
                        bufferPtr, headerPtr, 2);
                }
            }

            // Read remaining packet data
            await _stream.ReadExactlyAsync(
                System.MemoryExtensions.AsMemory(buffer, 2, length - 2),
                cancellationToken).ConfigureAwait(false);

            // Deserialize from buffer
            return TPacket.Deserialize(System.MemoryExtensions.AsSpan(buffer, 0, length));
        }
        finally
        {
            System.Buffers.ArrayPool<System.Byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Ultra-fast synchronous receive with maximum unsafe optimizations.
    /// Use this when you need absolute maximum performance and can guarantee thread safety.
    /// </summary>
    /// <returns>The deserialized packet implementing <see cref="IPacket"/>.</returns>
    /// <remarks>
    /// This method bypasses some safety checks for maximum performance.
    /// Only use in performance-critical scenarios where safety is guaranteed.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public unsafe TPacket ReceiveUnsafe()
    {
        // Direct stackalloc without bounds checking
        System.Byte* headerPtr = stackalloc System.Byte[2];

        // Read header directly into unsafe memory
        System.Span<System.Byte> headerSpan = new(headerPtr, 2);
        _stream.ReadExactly(headerSpan);

        // Ultra-fast big-endian conversion
        System.UInt16 length = (System.UInt16)((headerPtr[0] << 8) | headerPtr[1]);

        if (length <= PacketConstants.StackAllocLimit)
        {
            // Stack allocation for small packets
            System.Byte* bufferPtr = stackalloc System.Byte[length];

            // Direct memory copy - no bounds checking
            System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(
                bufferPtr, headerPtr, 2);

            // Read remaining data
            System.Span<System.Byte> remainingSpan = new(bufferPtr + 2, length - 2);
            _stream.ReadExactly(remainingSpan);

            // Create span from unsafe pointer
            System.Span<System.Byte> packetSpan = new(bufferPtr, length);
            return TPacket.Deserialize(packetSpan);
        }

        // For larger packets, still use ArrayPool but with unsafe optimizations
        System.Byte[] buffer = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(length);
        try
        {
            fixed (System.Byte* bufferPtr = buffer)
            {
                // Ultra-fast header copy
                System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(
                    bufferPtr, headerPtr, 2);
            }

            _stream.ReadExactly(buffer, 2, length - 2);
            return TPacket.Deserialize(System.MemoryExtensions.AsSpan(buffer, 0, length));
        }
        finally
        {
            System.Buffers.ArrayPool<System.Byte>.Shared.Return(buffer);
        }
    }
}