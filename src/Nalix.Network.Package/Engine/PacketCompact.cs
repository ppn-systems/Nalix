using Nalix.Common.Exceptions;
using Nalix.Common.Package.Enums;
using Nalix.Shared.LZ4;
using Nalix.Shared.LZ4.Encoders;
using Nalix.Shared.LZ4.Internal;

namespace Nalix.Network.Package.Engine;

/// <summary>
/// Provides helper methods for compressing and decompressing packet payloads.
/// </summary>
public static class PacketCompact
{
    /// <summary>
    /// Compresses the payload of the given packet if it exceeds a minimum size threshold.
    /// If compression is not beneficial (i.e., the compressed payload is larger),
    /// or if the payload is too small, the original packet is returned unchanged.
    /// </summary>
    /// <param name="packet">The packet whose payload needs to be compressed.</param>
    /// <returns>
    /// A new <see cref="Packet"/> instance with the compressed payload,
    /// or the original packet if compression is not applicable.
    /// </returns>
    /// <exception cref="PackageException">
    /// Thrown if the packet is empty, encrypted, or if an error occurs during compression.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static Packet Compress(in Packet packet)
    {
        if (packet.Payload.IsEmpty)
            throw new PackageException("Cannot compress an empty payload.");

        if ((packet.Flags & PacketFlags.Encrypted) != 0)
            throw new PackageException("Payload is encrypted and cannot be compressed.");

        if (packet.Payload.Length < 512)
            return packet;

        try
        {
            System.Memory<byte> bytes = CompressLZ4(packet.Payload.Span);

            if (bytes.Length >= packet.Payload.Length)
                return packet;

            return new Packet(
                packet.Id, packet.Number, packet.Checksum, packet.Timestamp,
                packet.Type, packet.Flags | PacketFlags.Compressed,
                packet.Priority, bytes, true);
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

        if (!((packet.Flags & PacketFlags.Compressed) != 0))
            return packet;

        try
        {
            return new Packet(
                packet.Id, packet.Number, packet.Checksum, packet.Timestamp,
                packet.Type, packet.Flags & ~PacketFlags.Compressed,
                packet.Priority, DecompressLZ4(packet.Payload.Span), true);
        }
        catch (System.Exception ex)
        {
            throw new PackageException("Error occurred during payload decompression.", ex);
        }
    }

    #region Private Methods

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Memory<byte> CompressLZ4(System.ReadOnlySpan<byte> input)
    {
        // Estimate worst case size: input.Length + header + worst-case expansion
        int size = Header.Size + LZ4Encoder.GetMaxLength(input.Length);
        byte[] buffer = new byte[size];

        int lenght = LZ4Codec.Encode(input, buffer);

        if (lenght < 0)
            throw new PackageException("Compression failed due to insufficient buffer size.");

        return System.MemoryExtensions.AsMemory(buffer, 0, lenght);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Memory<byte> DecompressLZ4(System.ReadOnlySpan<byte> input)
    {
        if (input.Length < Header.Size)
            throw new PackageException("Compressed payload too small to contain a valid header.");

        if (!LZ4Codec.Decode(input, out byte[]? buffer, out int written))
            throw new PackageException("Failed to decompress payload.");

        return System.MemoryExtensions.AsMemory(buffer, 0, written);
    }

    #endregion Private Methods
}
