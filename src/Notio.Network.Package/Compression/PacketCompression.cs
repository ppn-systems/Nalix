using Notio.Common.Exceptions;
using Notio.Common.Package;
using Notio.Extensions.Primitives;
using Notio.Utilities;
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace Notio.Network.Package.Compression;

/// <summary>
/// Provides helper methods for compressing and decompressing packet payloads.
/// </summary>
public static class PacketCompression
{
    /// <summary>
    /// Compresses the payload of the given packet.
    /// </summary>
    /// <param name="packet">The packet whose payload needs to be compressed.</param>
    /// <returns>A new <see cref="Packet"/> instance with the compressed payload.</returns>
    /// <exception cref="PackageException">
    /// Thrown if the packet is not eligible for compression, or if an error occurs during compression.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet CompressPayload(in Packet packet)
    {
        if (packet.Payload.IsEmpty)
            throw new PackageException("Cannot compress an empty payload.");

        if (packet.Flags.HasFlag(PacketFlags.Encrypted))
            throw new PackageException("Payload is encrypted and cannot be compressed.");

        try
        {
            byte[] compressedData = BrotliCompressor.Compress(packet.Payload);

            return new Packet(packet.Id, packet.Checksum, packet.Timestamp, packet.Number,
                packet.Type, packet.Flags.AddFlag(PacketFlags.Compressed), packet.Priority, compressedData);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            throw new PackageException("Error occurred during payload compression.", ex);
        }
    }

    /// <summary>
    /// Decompresses the payload of the given packet.
    /// </summary>
    /// <param name="packet">The packet whose payload needs to be decompressed.</param>
    /// <returns>A new <see cref="Packet"/> instance with the decompressed payload.</returns>
    /// <exception cref="PackageException">
    /// Thrown if the packet is not marked as compressed, if the payload is empty or null,
    /// or if an error occurs during decompression.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet DecompressPayload(in Packet packet)
    {
        if (packet.Payload.IsEmpty)
            throw new PackageException("Cannot compress an empty payload.");

        if (!packet.Flags.HasFlag(PacketFlags.Compressed))
            throw new PackageException("Payload is not marked as compressed.");

        try
        {
            byte[] decompressedData = BrotliCompressor.Decompress(packet.Payload);

            return new Packet(packet.Id, packet.Checksum, packet.Timestamp, packet.Number, packet.Type,
                packet.Flags.RemoveFlag(PacketFlags.Compressed), packet.Priority, decompressedData);
        }
        catch (InvalidDataException ex)
        {
            throw new PackageException("Invalid compressed data.", ex);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            throw new PackageException("Error occurred during payload decompression.", ex);
        }
    }
}
