using Notio.Common.Exceptions;
using Notio.Network.Package.Metadata;
using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Notio.Network.Package.Utilities;

[SkipLocalsInit]
public static class PacketSerializer
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WritePacketFast(Span<byte> buffer, in Packet packet)
    {
        try
        {
            int requiredSize = PacketSize.Header + packet.Payload.Length;
            if (buffer.Length < requiredSize)
                throw new PackageException("Buffer size is too small to write the packet.");

            ref byte bufferStart = ref MemoryMarshal.GetReference(buffer);

            Unsafe.WriteUnaligned(ref bufferStart, (ushort)requiredSize);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref bufferStart, PacketOffset.Id), packet.Id);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref bufferStart, PacketOffset.Type), packet.Type);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref bufferStart, PacketOffset.Flags), packet.Flags);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref bufferStart, PacketOffset.Priority), packet.Priority);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref bufferStart, PacketOffset.Command), packet.Command);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref bufferStart, PacketOffset.Timestamp), packet.Timestamp);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref bufferStart, PacketOffset.Checksum), packet.Checksum);

            packet.Payload.Span.CopyTo(buffer.Slice(PacketSize.Header, packet.Payload.Length));
        }
        catch (Exception ex) when (ex is not PackageException)
        {
            throw new PackageException("An error occurred while writing the packet.", ex);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet ReadPacketFast(ReadOnlySpan<byte> data)
    {
        try
        {
            if (data.Length < PacketSize.Header)
                throw new PackageException("Data size is smaller than the minimum header size.");

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

    public static ValueTask WritePacketFastAsync(Memory<byte> buffer, Packet packet)
        => new(Task.Run(() => WritePacketFast(buffer.Span, packet)));

    public static ValueTask<Packet> ReadPacketFastAsync(ReadOnlyMemory<byte> data)
        => new(Task.Run(() => ReadPacketFast(data.Span)));

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