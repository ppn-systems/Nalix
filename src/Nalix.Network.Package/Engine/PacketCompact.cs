using Nalix.Common.Exceptions;
using Nalix.Common.Package.Enums;
using Nalix.Extensions.Primitives;
using Nalix.Shared.LZ4;
using Nalix.Shared.LZ4.Internal;

namespace Nalix.Network.Package.Engine;

/// <summary>
/// Provides helper methods for compressing and decompressing packet payloads.
/// </summary>
public static class PacketCompact
{
    /// <summary>
    /// Compresses the payload of the given packet using the specified compression type.
    /// </summary>
    /// <param name="packet">The packet whose payload needs to be compressed.</param>
    /// <returns>A new <see cref="Packet"/> instance with the compressed payload.</returns>
    /// <exception cref="PackageException">
    /// Thrown if the packet is not eligible for compression, or if an error occurs during compression.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static Packet Compress(in Packet packet)
    {
        if (packet.Payload.IsEmpty)
            throw new PackageException("Cannot compress an empty payload.");

        if (packet.Flags.HasFlag(PacketFlags.Encrypted))
            throw new PackageException("Payload is encrypted and cannot be compressed.");

        try
        {
            return new Packet(
                packet.Id, packet.Checksum, packet.Timestamp, packet.Code,
                packet.Type, packet.Flags.AddFlag(PacketFlags.Compressed),
                packet.Priority, packet.Number, CompressLZ4(packet.Payload.Span), true);
        }
        catch (System.Exception ex)
        {
            throw new PackageException("Error occurred during payload compression.", ex);
        }
    }

    /// <summary>
    /// Decompresses the payload of the given packet using the specified compression type.
    /// </summary>
    /// <param name="packet">The packet whose payload needs to be decompressed.</param>
    /// <returns>A new <see cref="Packet"/> instance with the decompressed payload.</returns>
    /// <exception cref="PackageException">
    /// Thrown if the packet is not marked as compressed, if the payload is empty or null,
    /// or if an error occurs during decompression.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static Packet Decompress(in Packet packet)
    {
        if (packet.Payload.IsEmpty)
            throw new PackageException("Cannot decompress an empty payload.");

        if (!packet.Flags.HasFlag(PacketFlags.Compressed))
            throw new PackageException("Payload is not marked as compressed.");

        try
        {
            return new Packet(
                packet.Id, packet.Checksum, packet.Timestamp, packet.Code,
                packet.Type, packet.Flags.AddFlag(PacketFlags.Compressed),
                packet.Priority, packet.Number, DecompressLZ4(packet.Payload.Span), true);
        }
        catch (System.Exception ex)
        {
            throw new PackageException("Error occurred during payload decompression.", ex);
        }
    }

    #region Private Methods

    private static System.Memory<byte> CompressLZ4(System.ReadOnlySpan<byte> input)
    {
        // Estimate worst case size: input.Length + header + worst-case expansion
        int maxCompressedSize = Header.Size + input.Length + (input.Length / 255) + 16;
        byte[] buffer = new byte[maxCompressedSize];

        int compressedLength = LZ4Codec.Encode(input, buffer);

        if (compressedLength < 0)
            throw new PackageException("Compression failed due to insufficient buffer size.");

        return System.MemoryExtensions.AsMemory(buffer, 0, compressedLength);
    }

    private static System.Memory<byte> DecompressLZ4(System.ReadOnlySpan<byte> input)
    {
        if (input.Length < Header.Size)
            throw new PackageException("Compressed payload too small to contain a valid header.");

        Header header = MemOps.ReadUnaligned<Header>(input);

        if (header.OriginalLength < 0 || header.CompressedLength != input.Length)
            throw new PackageException("Invalid compressed data header.");

        byte[] buffer = new byte[header.OriginalLength];

        int decompressedLength = LZ4Codec.Decode(input, buffer);

        if (decompressedLength < 0)
            throw new PackageException("Decompression failed due to invalid data.");

        return System.MemoryExtensions.AsMemory(buffer, 0, decompressedLength);
    }

    #endregion Private Methods
}
