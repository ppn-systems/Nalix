using Notio.Common.Exceptions;
using Notio.Network.Package.Metadata;
using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Notio.Network.Package.Utilities;

/// <summary>
/// Provides methods for serializing and deserializing packets.
/// </summary>
[SkipLocalsInit]
public static class PacketSerializer
{
    private static readonly ArrayPool<byte> SharedPool = ArrayPool<byte>.Shared;

    /// <summary>
    /// Writes a packet to a given buffer in a fast and efficient way.
    /// </summary>
    /// <param name="buffer">The buffer to write the packet data to.</param>
    /// <param name="packet">The packet to be written.</param>
    /// <exception cref="PackageException">Thrown if the buffer size is too small or any other error occurs during writing.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WritePacketFast(Span<byte> buffer, in Packet packet)
    {
        if (buffer.Length < (PacketSize.Header + packet.Payload.Length))
            throw new PackageException("Buffer size is too small to write the packet.");

        try
        {
            PacketHeader header = new(packet);
            MemoryMarshal.Write(buffer, in header);

            packet.Payload.Span.CopyTo(buffer[PacketSize.Header..]);
        }
        catch (Exception ex) when (ex is not PackageException)
        {
            throw new PackageException("An error occurred while writing the packet.", ex);
        }
    }

    /// <summary>
    /// Reads a packet from a given data span in a fast and efficient way.
    /// </summary>
    /// <param name="data">The data span to read the packet from.</param>
    /// <returns>The deserialized packet.</returns>
    /// <exception cref="PackageException">Thrown if the data size is too small or any other error occurs during reading.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet ReadPacketFast(ReadOnlySpan<byte> data)
    {
        if (data.Length < PacketSize.Header)
            throw new PackageException("Data size is smaller than the minimum header size.");

        try
        {
            ref byte dataRef = ref MemoryMarshal.GetReference(data);

            ushort length = Unsafe.As<byte, ushort>(ref dataRef);
            if (length < PacketSize.Header || length > data.Length)
                throw new PackageException($"Invalid packet length: {length}. Must be between {PacketSize.Header} and {data.Length}.");

            byte id = Unsafe.Add(ref dataRef, PacketOffset.Id);
            byte type = Unsafe.Add(ref dataRef, PacketOffset.Type);
            byte flags = Unsafe.Add(ref dataRef, PacketOffset.Flags);
            byte priority = Unsafe.Add(ref dataRef, PacketOffset.Priority);
            ushort command = Unsafe.As<byte, ushort>(ref Unsafe.Add(ref dataRef, PacketOffset.Command));
            ulong timestamp = Unsafe.As<byte, ulong>(ref Unsafe.Add(ref dataRef, PacketOffset.Timestamp));
            uint checksum = Unsafe.As<byte, uint>(ref Unsafe.Add(ref dataRef, PacketOffset.Checksum));

            Memory<byte> payload = data[PacketSize.Header..length].ToArray();

            return new Packet(id, type, flags, priority, command, timestamp, checksum, payload);
        }
        catch (Exception ex) when (ex is not PackageException)
        {
            throw new PackageException("An error occurred while reading the packet.", ex);
        }
    }

    /// <summary>
    /// Asynchronously writes a packet to a given memory buffer.
    /// </summary>
    /// <param name="buffer">The buffer to write the packet data to.</param>
    /// <param name="packet">The packet to be written.</param>
    /// <returns>A value task representing the asynchronous write operation.</returns>
    public static ValueTask WritePacketFastAsync(Memory<byte> buffer, Packet packet)
        => new(Task.Run(() => WritePacketFast(buffer.Span, packet)));

    /// <summary>
    /// Asynchronously reads a packet from a given memory data.
    /// </summary>
    /// <param name="data">The data to read the packet from.</param>
    /// <returns>A value task representing the asynchronous read operation, returning the deserialized packet.</returns>
    public static ValueTask<Packet> ReadPacketFastAsync(ReadOnlyMemory<byte> data)
        => new(Task.Run(() => ReadPacketFast(data.Span)));

    /// <summary>
    /// Asynchronously writes a packet to a stream.
    /// </summary>
    /// <param name="stream">The stream to write the packet to.</param>
    /// <param name="packet">The packet to be written.</param>
    /// <returns>A task representing the asynchronous write operation.</returns>
    public static async Task WriteToStreamAsync(Stream stream, Packet packet)
    {
        int totalSize = PacketSize.Header + packet.Payload.Length;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(totalSize);

        try
        {
            WritePacketFast(buffer.AsSpan(0, totalSize), packet);
            await stream.WriteAsync(buffer.AsMemory(0, totalSize));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Asynchronously reads a packet from a stream.
    /// </summary>
    /// <param name="stream">The stream to read the packet from.</param>
    /// <returns>A task representing the asynchronous read operation, returning the deserialized packet.</returns>
    /// <exception cref="PackageException">Thrown if any error occurs during reading from the stream.</exception>
    public static async Task<Packet> ReadFromStreamAsync(Stream stream)
    {
        byte[] headerBuffer = ArrayPool<byte>.Shared.Rent(PacketSize.Header);

        try
        {
            int bytesRead = await stream.ReadAsync(headerBuffer.AsMemory(0, PacketSize.Header));
            if (bytesRead < PacketSize.Header)
                throw new PackageException("Failed to read the packet header.");

            ushort length = MemoryMarshal.Read<ushort>(headerBuffer);
            if (length < PacketSize.Header)
                throw new PackageException($"Invalid packet length: {length}.");

            byte[] fullBuffer = ArrayPool<byte>.Shared.Rent(length);
            Array.Copy(headerBuffer, fullBuffer, PacketSize.Header);

            bytesRead = await stream.ReadAsync(fullBuffer.AsMemory(PacketSize.Header, length - PacketSize.Header));
            if (bytesRead < length - PacketSize.Header)
                throw new PackageException("Failed to read the full packet.");

            return ReadPacketFast(fullBuffer.AsSpan(0, length));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(headerBuffer);
        }
    }
}
