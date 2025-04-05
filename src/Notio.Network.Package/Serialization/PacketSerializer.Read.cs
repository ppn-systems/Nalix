using Notio.Common.Exceptions;
using Notio.Common.Package.Metadata;
using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Network.Package.Serialization;

public static partial class PacketSerializer
{
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
            throw new PackageException(
                $"Data size ({data.Length}) is smaller than the minimum header size ({PacketSize.Header}).");

        try
        {
            // Read packet length and validate
            ushort length = MemoryMarshal.Read<ushort>(data[PacketSize.Length..]);

            if (length < PacketSize.Header)
                throw new PackageException($"Invalid packet length: {length}. Must be at least {PacketSize.Header}.");

            if (length > data.Length)
                throw new PackageException($"Packet length ({length}) exceeds available data ({data.Length}).");

            ushort id = MemoryMarshal.Read<ushort>(data[PacketOffset.Id..]);
            uint checksum = MemoryMarshal.Read<uint>(data[PacketOffset.Checksum..]);
            ulong timestamp = MemoryMarshal.Read<ulong>(data[PacketOffset.Timestamp..]);
            ushort code = MemoryMarshal.Read<ushort>(data[PacketOffset.Code..]);

            // Extract header fields more efficiently using direct span access
            byte number = data[PacketOffset.Number];
            byte type = data[PacketOffset.Type];
            byte flags = data[PacketOffset.Flags];
            byte priority = data[PacketOffset.Priority];

            // Create payload - optimize for zero-copy when possible
            MaterializePayloadFast(data[PacketSize.Header..], (length - PacketSize.Header), out Memory<byte> payload);

            return new Packet(id, checksum, timestamp, code, number, type, flags, priority, payload);
        }
        catch (Exception ex) when (ex is not PackageException)
        {
            throw new PackageException("Failed to deserialize packet", ex);
        }
    }

    /// <summary>
    /// Reads a packet from a given data span using unsafe direct memory access.
    /// </summary>
    /// <param name="data">The data span to read the packet from.</param>
    /// <returns>The deserialized packet.</returns>
    /// <exception cref="PackageException">Thrown if the data is invalid or corrupted.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Packet ReadPacketFastUnsafe(ReadOnlySpan<byte> data)
    {
        if (data.Length < PacketSize.Header)
            throw new PackageException(
                $"Data size ({data.Length}) is smaller than the minimum header size ({PacketSize.Header}).");

        try
        {
            // Use unsafe direct pointer access for header
            fixed (byte* pData = data)
            {
                // Read entire header at once instead of field by field
                PacketHeader* pHeader = (PacketHeader*)pData;

                ushort length = pHeader->Length;

                // Validate packet length
                if (length < PacketSize.Header)
                    throw new PackageException($"Invalid packet length: {length}. Must be at least {PacketSize.Header}.");

                if (length > data.Length)
                    throw new PackageException($"Packet length ({length}) exceeds available data ({data.Length}).");

                // Extract all fields from the header in one go
                ushort id = pHeader->Id;
                uint checksum = pHeader->Checksum;
                ulong timestamp = pHeader->Timestamp;
                ushort code = pHeader->Code;
                byte number = pHeader->Number;
                byte type = pHeader->Type;
                byte flags = pHeader->Flags;
                byte priority = pHeader->Priority;

                // Create payload efficiently
                MaterializePayloadFastUnsafe(data[PacketSize.Header..], (length - PacketSize.Header), out Memory<byte> payload);

                return new Packet(id, checksum, timestamp, code, number, type, flags, priority, payload);
            }
        }
        catch (Exception ex) when (ex is not PackageException)
        {
            throw new PackageException("Failed to deserialize packet", ex);
        }
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
        byte[] headerBuffer = RentHeaderBuffer();

        try
        {
            // Read the packet header
            int bytesRead = await stream.ReadAtLeastAsync(headerBuffer, PacketSize.Header, throwOnEndOfStream: true, cancellationToken);

            if (bytesRead < PacketSize.Header)
                throw new PackageException(
                    $"Failed to read the packet header. Got {bytesRead} bytes instead of {PacketSize.Header}.");

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
