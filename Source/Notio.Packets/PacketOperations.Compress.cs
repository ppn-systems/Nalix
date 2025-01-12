using Notio.Packets.Enums;
using Notio.Packets.Exceptions;
using Notio.Packets.Extensions;
using System.IO.Compression;
using System.IO;
using System.Runtime.CompilerServices;
using System;

namespace Notio.Packets;

public static partial class PacketOperations
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet CompressPayload(this in Packet packet)
    {
        if (packet.Payload.IsEmpty)
            throw new PacketException("Cannot compress an empty payload.");

        if (packet.Flags.HasFlag(PacketFlags.IsEncrypted))
            throw new PacketException("Payload is encrypted and cannot be compressed.");

        try
        {
            // Ước tính kích thước ban đầu để tránh việc phải mở rộng buffer
            int estimatedCompressedSize = packet.Payload.Length / 2;
            using MemoryStream outputStream = new(estimatedCompressedSize);

            // Sử dụng using declaration để tự động dispose
            using GZipStream gzipStream = new(outputStream, CompressionLevel.Optimal, leaveOpen: true);

            // Sử dụng WriteAsync với ReadOnlyMemory để tối ưu
            gzipStream.Write(packet.Payload.Span);

            return new Packet(
                packet.Type,
                packet.Flags.AddFlag(PacketFlags.IsCompressed),
                packet.Command,
                outputStream.ToArray()
            );
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            throw new PacketException("Error occurred during payload compression.", ex);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet DecompressPayload(this in Packet packet)
    {
        if (packet.Payload.IsEmpty)
            throw new PacketException("Cannot decompress an empty payload.");

        if (!packet.Flags.HasFlag(PacketFlags.IsCompressed))
            throw new PacketException("Payload is not marked as compressed.");

        if (packet.Flags.HasFlag(PacketFlags.IsEncrypted))
            throw new PacketException("Payload is encrypted and cannot be decompressed.");

        try
        {
            // Ước tính kích thước giải nén để tối ưu bộ nhớ
            int estimatedDecompressedSize = packet.Payload.Length * 4;
            using MemoryStream inputStream = new(packet.Payload.ToArray());
            using MemoryStream outputStream = new(estimatedDecompressedSize);

            using GZipStream gzipStream = new(inputStream, CompressionMode.Decompress);

            // Sử dụng buffer được định nghĩa trước
            gzipStream.CopyTo(outputStream, Packet.MaxPacketSize / 2);

            return new Packet(
                packet.Type,
                packet.Flags.RemoveFlag(PacketFlags.IsCompressed),
                packet.Command,
                outputStream.ToArray()
            );
        }
        catch (InvalidDataException ex)
        {
            throw new PacketException("Invalid compressed data.", ex);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            throw new PacketException("Error occurred during payload decompression.", ex);
        }
    }
}