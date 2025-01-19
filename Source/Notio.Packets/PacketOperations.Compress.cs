using Notio.Common.Exceptions;
using Notio.Packets.Enums;
using Notio.Packets.Extensions;
using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;

namespace Notio.Packets;

/// <summary>
/// Provides operations for compressing and decompressing packets.
/// </summary>
public static partial class PacketOperations
{
    /// <summary>
    /// Compresses the payload of the packet.
    /// </summary>
    /// <param name="packet">The packet whose payload is to be compressed.</param>
    /// <returns>A new packet with the compressed payload.</returns>
    /// <exception cref="PacketException">Thrown when an error occurs during compression.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet CompressPayload(this in Packet packet)
    {
        packet.ValidatePacketForCompression();

        try
        {
            int estimatedCompressedSize = Math.Max(128, packet.Payload.Length / 2);
            using MemoryStream outputStream = new(estimatedCompressedSize);
            using (GZipStream gzipStream = new(outputStream, CompressionLevel.Optimal, leaveOpen: true))
            {
                gzipStream.Write(packet.Payload.Span);
                gzipStream.Flush();
            }

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

    /// <summary>
    /// Decompresses the payload of the packet.
    /// </summary>
    /// <param name="packet">The packet whose payload is to be decompressed.</param>
    /// <returns>A new packet with the decompressed payload.</returns>
    /// <exception cref="PacketException">Thrown when an error occurs during decompression or if the payload is not marked as compressed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet DecompressPayload(this in Packet packet)
    {
        packet.ValidatePacketForCompression();

        if (!packet.Flags.HasFlag(PacketFlags.IsCompressed))
            throw new PacketException("Payload is not marked as compressed.");

        try
        {
            int estimatedDecompressedSize = Math.Max(128, packet.Payload.Length * 4);
            using MemoryStream inputStream = new(packet.Payload.ToArray());
            using MemoryStream outputStream = new(estimatedDecompressedSize);
            using (GZipStream gzipStream = new(inputStream, CompressionMode.Decompress))
            {
                gzipStream.CopyTo(outputStream, 8192);
            }

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

    /// <summary>
    /// Tries to perform an operation on the packet.
    /// </summary>
    /// <param name="packet">The packet on which the operation is to be performed.</param>
    /// <param name="operation">The operation to be performed.</param>
    /// <param name="result">The result of the operation.</param>
    /// <returns><c>true</c> if the operation succeeded; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCompress(this Packet packet, Func<Packet, Packet> operation, out Packet result)
    {
        try
        {
            result = operation(packet);
            return true;
        }
        catch (PacketException)
        {
            result = default;
            return false;
        }
    }

    /// <summary>
    /// Tries to compress the payload of the packet.
    /// </summary>
    /// <param name="packet">The packet whose payload is to be compressed.</param>
    /// <param name="compressPayload">The compressed packet.</param>
    /// <returns><c>true</c> if the payload was compressed successfully; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCompressPayload(this Packet packet, out Packet compressPayload)
        => packet.TryCompress(p => p.CompressPayload(), out compressPayload);

    /// <summary>
    /// Tries to decompress the payload of the packet.
    /// </summary>
    /// <param name="packet">The packet whose payload is to be decompressed.</param>
    /// <param name="deCompressPayload">The decompressed packet.</param>
    /// <returns><c>true</c> if the payload was decompressed successfully; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryDecompressPayload(this Packet packet, out Packet deCompressPayload)
        => packet.TryCompress(p => p.DecompressPayload(), out deCompressPayload);
}