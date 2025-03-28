using Notio.Common.Exceptions;
using Notio.Network.Package.Metadata;
using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Network.Package.Serialization;

/// <summary>
/// Provides high-performance methods for serializing and deserializing network packets.
/// </summary>
[SkipLocalsInit]
public static class PacketSerializer
{
    // Pre-allocated buffers for stream operations
    private static readonly ThreadLocal<byte[]> _threadLocalHeaderBuffer = new(
        () => new byte[PacketSize.Header], true);

    /// <summary>
    /// Writes a packet to a given buffer in a fast and efficient way.
    /// </summary>
    /// <param name="buffer">The buffer to write the packet data to.</param>
    /// <param name="packet">The packet to be written.</param>
    /// <returns>The number of bytes written to the buffer.</returns>
    /// <exception cref="PackageException">Thrown if the buffer size is too small for the packet.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WritePacketFast(Span<byte> buffer, in Packet packet)
    {
        int totalSize = PacketSize.Header + packet.Payload.Length;

        if (buffer.Length < totalSize)
            throw new PackageException($"Buffer size ({buffer.Length}) is too small for packet size ({totalSize}).");

        try
        {
            // Write the packet length first
            MemoryMarshal.Write(buffer, in totalSize);

            // Write the rest of the header fields
            buffer[PacketOffset.Id] = packet.Id;
            buffer[PacketOffset.Type] = (byte)packet.Type;
            buffer[PacketOffset.Flags] = (byte)packet.Flags;
            buffer[PacketOffset.Priority] = (byte)packet.Priority;

            ushort command = packet.Command;
            MemoryMarshal.Write(buffer[PacketOffset.Command..], in command);
            ulong timestamp = packet.Timestamp;
            MemoryMarshal.Write(buffer[PacketOffset.Timestamp..], in timestamp);
            uint checksum = packet.Checksum;
            MemoryMarshal.Write(buffer[PacketOffset.Checksum..], in checksum);
            // Copy payload data
            if (packet.Payload.Length > 0)
            {
                packet.Payload.Span.CopyTo(buffer[PacketSize.Header..]);
            }

            return totalSize;
        }
        catch (Exception ex) when (ex is not PackageException)
        {
            throw new PackageException("Failed to serialize packet", ex);
        }
    }

    /// <summary>
    /// Reads a packet from a given data span in a fast and efficient way.
    /// </summary>
    /// <param name="data">The data span to read the packet from.</param>
    /// <returns>The deserialized packet.</returns>
    /// <exception cref="PackageException">Thrown if the data is invalid or corrupted.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet ReadPacketFast(ReadOnlySpan<byte> data)
    {
        if (data.Length < PacketSize.Header)
            throw new PackageException($"Data size ({data.Length}) is smaller than the minimum header size ({PacketSize.Header}).");

        try
        {
            // Read packet length and validate
            ushort length = MemoryMarshal.Read<ushort>(data);

            if (length < PacketSize.Header)
                throw new PackageException($"Invalid packet length: {length}. Must be at least {PacketSize.Header}.");

            if (length > data.Length)
                throw new PackageException($"Packet length ({length}) exceeds available data ({data.Length}).");

            // Extract header fields more efficiently using direct span access
            byte id = data[PacketOffset.Id];
            byte type = data[PacketOffset.Type];
            byte flags = data[PacketOffset.Flags];
            byte priority = data[PacketOffset.Priority];

            ushort command = MemoryMarshal.Read<ushort>(data[PacketOffset.Command..]);
            ulong timestamp = MemoryMarshal.Read<ulong>(data[PacketOffset.Timestamp..]);
            uint checksum = MemoryMarshal.Read<uint>(data[PacketOffset.Checksum..]);

            // Create payload - optimize for zero-copy when possible
            Memory<byte> payload;
            int payloadLength = length - PacketSize.Header;

            if (payloadLength > 0)
            {
                // Only allocate a new array if needed
                if (data is { IsEmpty: false } && MemoryMarshal.TryGetArray(
                    data[PacketSize.Header..length].ToArray(), out ArraySegment<byte> segment))
                {
                    payload = segment;
                }
                else
                {
                    // Fall back to copying the payload
                    byte[] payloadArray = new byte[payloadLength];
                    data.Slice(PacketSize.Header, payloadLength).CopyTo(payloadArray);
                    payload = payloadArray;
                }
            }
            else
            {
                payload = Memory<byte>.Empty;
            }

            return new Packet(id, type, flags, priority, command, timestamp, checksum, payload);
        }
        catch (Exception ex) when (ex is not PackageException)
        {
            throw new PackageException("Failed to deserialize packet", ex);
        }
    }

    /// <summary>
    /// Asynchronously writes a packet to a given memory buffer.
    /// </summary>
    /// <param name="buffer">The buffer to write the packet data to.</param>
    /// <param name="packet">The packet to be written.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>A value task representing the asynchronous write operation.</returns>
    public static ValueTask<int> WritePacketFastAsync(
        Memory<byte> buffer, Packet packet, CancellationToken cancellationToken = default)
    {
        // For small payloads, perform synchronously to avoid task overhead
        if (packet.Payload.Length < 4096)
        {
            try
            {
                return new ValueTask<int>(WritePacketFast(buffer.Span, packet));
            }
            catch (Exception ex) when (ex is not OperationCanceledException && cancellationToken.IsCancellationRequested)
            {
                return ValueTask.FromCanceled<int>(cancellationToken);
            }
            catch (Exception ex)
            {
                return ValueTask.FromException<int>(ex);
            }
        }

        // For larger payloads, use Task to prevent blocking
        return new ValueTask<int>(Task.Run(() => WritePacketFast(buffer.Span, packet), cancellationToken));
    }

    /// <summary>
    /// Asynchronously reads a packet from a given memory data.
    /// </summary>
    /// <param name="data">The data to read the packet from.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>A value task representing the asynchronous read operation, returning the deserialized packet.</returns>
    public static ValueTask<Packet> ReadPacketFastAsync(
        ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        // For small data, perform synchronously to avoid task overhead
        if (data.Length < 4096)
        {
            try
            {
                return new ValueTask<Packet>(ReadPacketFast(data.Span));
            }
            catch (Exception ex) when (ex is not OperationCanceledException && cancellationToken.IsCancellationRequested)
            {
                return ValueTask.FromCanceled<Packet>(cancellationToken);
            }
            catch (Exception ex)
            {
                return ValueTask.FromException<Packet>(ex);
            }
        }

        // For larger data, use Task to prevent blocking
        return new ValueTask<Packet>(Task.Run(() => ReadPacketFast(data.Span), cancellationToken));
    }

    /// <summary>
    /// Asynchronously writes a packet to a stream.
    /// </summary>
    /// <param name="stream">The stream to write the packet to.</param>
    /// <param name="packet">The packet to be written.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>A task representing the asynchronous write operation.</returns>
    public static async ValueTask WriteToStreamAsync(
        Stream stream, Packet packet, CancellationToken cancellationToken = default)
    {
        int totalSize = PacketSize.Header + packet.Payload.Length;

        // For very large payloads, rent from the pool
        byte[] buffer = totalSize <= 81920
            ? ArrayPool<byte>.Shared.Rent(totalSize)
            : new byte[totalSize]; // For extremely large packets, avoid exhausting the pool

        try
        {
            int bytesWritten = WritePacketFast(buffer.AsSpan(0, totalSize), packet);

            await stream.WriteAsync(new ReadOnlyMemory<byte>(buffer, 0, bytesWritten), cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
        finally
        {
            if (totalSize <= 81920)
            {
                // Only return to pool if we rented from it
                ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
            }
        }
    }

    /// <summary>
    /// Asynchronously reads a packet from a stream.
    /// </summary>
    /// <param name="stream">The stream to read the packet from.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>A task representing the asynchronous read operation, returning the deserialized packet.</returns>
    /// <exception cref="PackageException">Thrown if any error occurs during reading from the stream.</exception>
    public static async ValueTask<Packet> ReadFromStreamAsync(
        Stream stream, CancellationToken cancellationToken = default)
    {
        // Use thread-local buffer for the header to reduce allocations
        byte[] headerBuffer = _threadLocalHeaderBuffer.Value!;

        try
        {
            // Read the packet header
            int bytesRead = await stream.ReadAtLeastAsync(headerBuffer, PacketSize.Header, throwOnEndOfStream: true, cancellationToken);

            if (bytesRead < PacketSize.Header)
                throw new PackageException($"Failed to read the packet header. Got {bytesRead} bytes instead of {PacketSize.Header}.");

            // Read the packet length from the header
            ushort length = MemoryMarshal.Read<ushort>(headerBuffer);

            if (length < PacketSize.Header)
                throw new PackageException($"Invalid packet length: {length}. Must be at least {PacketSize.Header}.");

            int payloadSize = length - PacketSize.Header;

            if (payloadSize <= 0)
            {
                // No payload, just return a packet constructed from the header
                return ReadPacketFast(headerBuffer.AsSpan(0, PacketSize.Header));
            }

            // For payloads, optimize based on size
            if (length <= 8192) // Use shared buffer pool for reasonably sized packets
            {
                byte[] fullBuffer = ArrayPool<byte>.Shared.Rent(length);
                try
                {
                    // Copy header to the full buffer
                    Buffer.BlockCopy(headerBuffer, 0, fullBuffer, 0, PacketSize.Header);

                    // Read the payload directly into the buffer
                    bytesRead = await stream.ReadAtLeastAsync(
                        fullBuffer.AsMemory(PacketSize.Header, payloadSize),
                        payloadSize,
                        throwOnEndOfStream: true,
                        cancellationToken);

                    if (bytesRead < payloadSize)
                        throw new PackageException($"Failed to read the full packet payload. Got {bytesRead} bytes instead of {payloadSize}.");

                    return ReadPacketFast(fullBuffer.AsSpan(0, length));
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(fullBuffer, clearArray: true);
                }
            }
            else // For extremely large packets, avoid exhausting the pool
            {
                // Allocate a new buffer for the full packet
                byte[] fullBuffer = new byte[length];

                // Copy header to the full buffer
                Buffer.BlockCopy(headerBuffer, 0, fullBuffer, 0, PacketSize.Header);

                // Read the payload directly into the buffer
                bytesRead = await stream.ReadAtLeastAsync(
                    fullBuffer.AsMemory(PacketSize.Header, payloadSize),
                    payloadSize,
                    throwOnEndOfStream: true,
                    cancellationToken);

                if (bytesRead < payloadSize)
                    throw new PackageException($"Failed to read the full packet payload. Got {bytesRead} bytes instead of {payloadSize}.");

                return ReadPacketFast(fullBuffer);
            }
        }
        catch (Exception ex) when (ex is not PackageException and not OperationCanceledException)
        {
            throw new PackageException("Failed to read packet from stream", ex);
        }
    }
}
