using Notio.Packets.Enums;
using Notio.Packets.Extensions;
using Notio.Packets.Helpers;
using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;

namespace Notio.Packets;

public static partial class PacketOperations
{
    /// <summary>
    /// Nén Payload trong Packet và trả về Packet mới với payload đã nén.
    /// </summary>
    /// <exception cref="PacketException">Ném lỗi khi xảy ra lỗi nén payload.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet CompressPayload(this in Packet packet)
    {
        if (packet.Payload.IsEmpty)
        {
            throw new PacketException("Cannot compress an empty payload.");
        }

        try
        {
            using MemoryStream outputStream = new();
            using (GZipStream gzipStream = new(outputStream, CompressionLevel.Optimal, true))
            {
                gzipStream.Write(packet.Payload.Span);
            }

            // Tạo Packet mới với payload đã nén
            return new Packet(
                packet.Type,
                packet.Flags.AddFlag(PacketFlags.IsCompressed),
                packet.Command,
                outputStream.ToArray()
            );
        }
        catch (Exception ex) when (ex is IOException || ex is ObjectDisposedException)
        {
            throw new PacketException("Error occurred during payload compression.", ex);
        }
    }

    /// <summary>
    /// Giải nén Payload trong Packet và trả về Packet mới với payload đã giải nén.
    /// </summary>
    /// <exception cref="PacketException">Ném lỗi khi xảy ra lỗi giải nén payload.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet DecompressPayload(this in Packet packet)
    {
        if (packet.Payload.IsEmpty)
        {
            throw new PacketException("Cannot decompress an empty payload.");
        }

        if (!packet.Flags.HasFlag(PacketFlags.IsCompressed))
        {
            throw new PacketException("Payload is not marked as compressed.");
        }

        try
        {
            using MemoryStream inputStream = new(packet.Payload.ToArray());
            using MemoryStream outputStream = new();
            using (GZipStream gzipStream = new(inputStream, CompressionMode.Decompress))
            {
                gzipStream.CopyTo(outputStream, 81920);
            }

            // Tạo Packet mới với payload đã giải nén
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
        catch (Exception ex) when (ex is IOException || ex is ObjectDisposedException)
        {
            throw new PacketException("Error occurred during payload decompression.", ex);
        }
    }
}