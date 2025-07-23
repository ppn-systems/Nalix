using Nalix.Common.Exceptions;
using Nalix.Common.Packets;
using Nalix.Common.Packets.Metadata;
using Nalix.Network.Package.Engine;
using Nalix.Shared.Serialization;

namespace Nalix.Network.Package.Extensions;

/// <summary>
/// Provides extension methods for working with IPacket instances.
/// </summary>
public static class PacketExtensions
{
    /// <summary>
    /// Verifies if the checksum in the byte array matches the computed checksum.
    /// </summary>
    /// <param name="packet">The byte array representing the packet.</param>
    /// <returns>Returns true if the checksum is valid; otherwise, false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean IsChecksum(this System.Byte[] packet)
        => PacketOps.IsValidChecksum(packet);

    /// <summary>
    /// Serializes a <see cref="IPacket"/> into a byte array.
    /// </summary>
    /// <param name="packet">The packet to serialize.</param>
    /// <returns>A byte array representing the serialized packet.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Byte[] Serialize(this in Packet packet)
        => LiteSerializer.Serialize(in packet);

    /// <summary>
    /// Deserializes a packet from a <see cref="System.ReadOnlySpan{T}"/> of bytes.
    /// </summary>
    /// <param name="data">The byte span containing packet data.</param>
    /// <returns>A <see cref="IPacket"/> instance created from the data.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static IPacket Deserialize(this System.ReadOnlySpan<System.Byte> data)
    {
        IPacket? packet = default;
        _ = LiteSerializer.Deserialize(data, ref packet);
        return packet ?? throw new System.InvalidOperationException("Deserialization resulted in a null packet.");
    }

    /// <summary>
    /// Deserializes a packet from a <see cref="System.ReadOnlyMemory{T}"/> of bytes.
    /// </summary>
    /// <param name="data">The memory segment containing packet data.</param>
    /// <returns>A <see cref="IPacket"/> instance created from the data.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static IPacket Deserialize(this System.ReadOnlyMemory<System.Byte> data)
    {
        IPacket? packet = default;
        _ = LiteSerializer.Deserialize(data.Span, ref packet);
        return packet ?? throw new System.InvalidOperationException("Deserialization resulted in a null packet.");
    }

    /// <summary>
    /// Deserializes a packet from a byte array.
    /// </summary>
    /// <param name="data">The byte array containing packet data.</param>
    /// <returns>A <see cref="IPacket"/> instance created from the data.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static IPacket Deserialize(this System.Byte[] data)
    {
        IPacket? packet = default;
        _ = LiteSerializer.Deserialize(data, ref packet);
        return packet ?? throw new System.InvalidOperationException("Deserialization resulted in a null packet.");
    }

    /// <summary>
    /// Attempts to serialize a packet into a provided span of bytes.
    /// </summary>
    /// <param name="packet">The packet to serialize.</param>
    /// <param name="destination">The destination span where the serialized data will be stored.</param>
    /// <param name="bytesWritten">Outputs the ProtocolType of bytes written to the destination span.</param>
    /// <returns>True if serialization was successful; otherwise, false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean TrySerialize(
        this in Packet packet,
        System.Span<System.Byte> destination,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out System.Int32 bytesWritten)
    {
        System.Int32 payloadLength = packet.Payload.Length;
        System.Int32 totalSize = PacketSize.Header + payloadLength;

        // Validate before attempting to serialize
        if (payloadLength > System.UInt16.MaxValue || destination.Length < totalSize)
        {
            bytesWritten = 0;
            return false;
        }

        try
        {
            bytesWritten = LiteSerializer.Serialize(in packet, destination);
            return true;
        }
        catch (SerializationException) // Specific catch (optional)
        {
            bytesWritten = 0;
            return false;
        }
        catch (System.NotSupportedException)
        {
            bytesWritten = 0;
            return false;
        }
    }

    /// <summary>
    /// Attempts to deserialize a packet from a span of bytes.
    /// </summary>
    /// <param name="source">The byte span containing packet data.</param>
    /// <param name="packet">Outputs the deserialized packet if successful.</param>
    /// <returns>True if deserialization was successful; otherwise, false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean TryDeserialize(
        this System.ReadOnlySpan<System.Byte> source,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Packet packet)
    {
        packet = default;

        // Check if at least the header is present
        if (source.Length < PacketSize.Header)
        {
            return false;
        }

        // Read packet length from first 2 bytes (ushort)
        System.UInt16 length = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<System.UInt16>(
            ref System.Runtime.InteropServices.MemoryMarshal.GetReference(source));

        // Validate length is within expected range
        if (length < PacketSize.Header || length > source.Length)
        {
            return false;
        }

        // Safe deserialize without throwing
        try
        {
            System.Int32 bytesRead = LiteSerializer.Deserialize(source[..length], ref packet);
            return bytesRead == length;
        }
        catch
        {
            return false;
        }
    }
}