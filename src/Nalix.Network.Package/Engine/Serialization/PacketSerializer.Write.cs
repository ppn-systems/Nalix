using Nalix.Common.Exceptions;
using Nalix.Common.Package.Metadata;

namespace Nalix.Network.Package.Engine.Serialization;

public static partial class PacketSerializer
{
    #region Public Method

    /// <summary>
    /// Writes a packet to a given buffer in a fast and efficient way.
    /// </summary>
    /// <param name="buffer">The buffer to write the packet data to.</param>
    /// <param name="packet">The packet to be written.</param>
    /// <returns>The Number of bytes written to the buffer.</returns>
    /// <exception cref="PackageException">Thrown if the buffer size is too small for the packet.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static int WritePacket(System.Span<byte> buffer, in Packet packet)
    {
        int totalSize = PacketSize.Header + packet.Payload.Length;

        if (buffer.Length < totalSize)
            throw new PackageException($"Buffer size ({buffer.Length}) is too small for packet size ({totalSize}).");

        ushort id = packet.Id;
        long timestamp = packet.Timestamp;
        uint checksum = packet.Checksum;

        try
        {
            // Writing the first part of the header in one block (could optimize by grouping)
            System.Runtime.InteropServices.MemoryMarshal.Write(buffer, in totalSize);
            System.Runtime.InteropServices.MemoryMarshal.Write(buffer[PacketOffset.Id..], in id);
            System.Runtime.InteropServices.MemoryMarshal.Write(buffer[PacketOffset.Timestamp..], in timestamp);
            System.Runtime.InteropServices.MemoryMarshal.Write(buffer[PacketOffset.Checksum..], in checksum);

            // Writing the packet-specific fields
            buffer[PacketOffset.Number] = packet.Number;
            buffer[PacketOffset.Type] = (byte)packet.Type;
            buffer[PacketOffset.Flags] = (byte)packet.Flags;
            buffer[PacketOffset.Priority] = (byte)packet.Priority;

            // WriteInt16 the payload if it's not empty
            if (packet.Payload.Length > 0)
                packet.Payload.Span.CopyTo(buffer[PacketSize.Header..]);

            return totalSize;
        }
        catch (System.Exception ex) when (ex is not PackageException)
        {
            throw new PackageException("Failed to serialize packet", ex);
        }
    }

    #endregion Public Method

    #region Public Method Async

    /// <summary>
    /// Asynchronously writes a packet to a given memory buffer.
    /// </summary>
    /// <param name="buffer">The buffer to write the packet data to.</param>
    /// <param name="packet">The packet to be written.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>A value task representing the asynchronous write operation.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Threading.Tasks.ValueTask<int> WritePacketAsync(
        System.Memory<byte> buffer,
        Packet packet,
        System.Threading.CancellationToken cancellationToken = default)
    {
        // For small payloads, perform synchronously to avoid task overhead
        if (packet.Payload.Length < 4096)
        {
            try
            {
                return new System.Threading.Tasks.ValueTask<int>(WritePacket(buffer.Span, packet));
            }
            catch (System.OperationCanceledException)
            {
                return System.Threading.Tasks.ValueTask.FromCanceled<int>(cancellationToken);
            }
            catch (System.Exception ex)
            {
                return System.Threading.Tasks.ValueTask.FromException<int>(ex);
            }
        }

        // For larger payloads, use Task to prevent blocking
        return new System.Threading.Tasks.ValueTask<int>(
            System.Threading.Tasks.Task.Run(() => WritePacket(buffer.Span, packet), cancellationToken));
    }

    /// <summary>
    /// Asynchronously writes a packet to a stream.
    /// </summary>
    /// <param name="stream">The stream to write the packet to.</param>
    /// <param name="packet">The packet to be written.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>A task representing the asynchronous write operation.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static async System.Threading.Tasks.ValueTask WriteToStreamAsync(
        System.IO.Stream stream,
        Packet packet,
        System.Threading.CancellationToken cancellationToken = default)
    {
        int totalSize = PacketSize.Header + packet.Payload.Length;

        // For very large payloads, rent from the pool
        byte[] buffer = totalSize <= Threshold
            ? System.Buffers.ArrayPool<byte>.Shared.Rent(totalSize)
            : new byte[totalSize]; // For extremely large packets, avoid exhausting the pool

        try
        {
            int bytesWritten = WritePacket(System.MemoryExtensions.AsSpan(buffer, 0, totalSize), packet);

            await stream.WriteAsync(new System.ReadOnlyMemory<byte>(buffer, 0, bytesWritten), cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
        catch (System.OperationCanceledException ex)
        {
            // Handle cancellation gracefully
            throw new PackageException("Packet serialization was canceled.", ex);
        }
        catch (System.Exception ex)
        {
            // Re-throw any unexpected errors with extra context
            throw new PackageException("Failed to write packet to stream.", ex);
        }
        finally
        {
            // Only return to pool if we rented from it
            if (totalSize <= Threshold)
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
            }
        }
    }

    #endregion Public Method Async
}
