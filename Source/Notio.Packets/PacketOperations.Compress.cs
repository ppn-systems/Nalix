using Notio.Packets.Enums;
using Notio.Packets.Extensions;
using Notio.Packets.Helpers;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;

namespace Notio.Packets;

public static partial class PacketOperations
{
    /// <summary>
    /// Nén Payload trong Packet và cập nhật trực tiếp.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet CompressPayload(this in Packet packet)
    {
        using MemoryStream outputStream = new();

        using (GZipStream gzipStream = new(outputStream, CompressionLevel.Optimal, true))
            gzipStream.Write(packet.Payload.Span);

        // Tạo packet mới với payload đã nén
        return new Packet(
            packet.Type,
            packet.Flags.AddFlag(PacketFlags.IsCompressed),
            packet.Command,
            outputStream.ToArray()
        );
    }

    /// <summary>
    /// Giải nén Payload trong Packet và cập nhật trực tiếp.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet DecompressPayload(this in Packet packet)
    {
        using MemoryStream inputStream = new(packet.Payload.ToArray());
        using MemoryStream outputStream = new();

        try
        {
            using GZipStream gzipStream = new(inputStream, CompressionMode.Decompress);
            gzipStream.CopyTo(outputStream, 81920);

            return new Packet(
                packet.Type,
                packet.Flags,
                packet.Command,
                outputStream.ToArray()
            );
        }
        catch (InvalidDataException)
        {
            throw new PacketException("Invalid compressed data");
        }
    }
}