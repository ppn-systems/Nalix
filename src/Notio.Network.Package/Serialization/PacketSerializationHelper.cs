using Notio.Common.Exceptions;
using Notio.Common.Package.Metadata;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Notio.Network.Package.Serialization;

/// <summary>
/// Provides high-performance scaling methods for the IPacket class.
/// </summary>
[SkipLocalsInit]
public static class PacketSerializationHelper
{
    /// <summary>
    /// Serializes the specified packet to a byte array.
    /// </summary>
    /// <param name="packet">The packet to serialize.</param>
    /// <returns>The serialized byte array representing the packet.</returns>
    public static byte[] Serialize(in Packet packet)
    {
        int totalSize = PacketSize.Header + packet.Payload.Length;

        if (packet.Payload.Length > ushort.MaxValue)
            throw new PackageException("Payload is too large.");

        if (totalSize <= PacketConstants.StackAllocLimit)
        {
            Span<byte> stackBuffer = stackalloc byte[totalSize];
            PacketSerializer.WritePacketFast(stackBuffer, in packet);
            return stackBuffer.ToArray();
        }
        else
        {
            byte[] rentedArray = PacketConstants.SharedBytePool.Rent(totalSize);
            try
            {
                PacketSerializer.WritePacketFast(rentedArray.AsSpan(0, totalSize), in packet);
                return rentedArray.AsSpan(0, totalSize).ToArray();
            }
            finally
            {
                PacketConstants.SharedBytePool.Return(rentedArray, clearArray: true);
            }
        }
    }

    /// <summary>
    /// Deserializes the specified byte array to a packet.
    /// </summary>
    /// <param name="data">The byte array to deserialize.</param>
    /// <returns>The deserialized packet.</returns>
    public static Packet Deserialize(ReadOnlySpan<byte> data)
    {
        if (data.Length < PacketSize.Header)
            throw new PackageException("Invalid data length: smaller than header size.");

        short length = MemoryMarshal.Read<short>(data);
        if (length < PacketSize.Header || length > data.Length)
            throw new PackageException($"Invalid packet length: {length}.");

        return PacketSerializer.ReadPacketFast(data[..length]);
    }

    /// <summary>
    /// Deserializes the specified ReadOnlyMemory to a packet.
    /// </summary>
    /// <param name="data">The ReadOnlyMemory to deserialize.</param>
    /// <returns>The deserialized packet.</returns>
    public static Packet Deserialize(ReadOnlyMemory<byte> data)
        => Deserialize(data.Span);

    /// <summary>
    /// Deserializes the specified byte array to a packet.
    /// </summary>
    /// <param name="data">The byte array to deserialize.</param>
    /// <returns>The deserialized packet.</returns>
    public static Packet Deserialize(byte[] data)
        => Deserialize((ReadOnlySpan<byte>)data);

    /// <summary>
    /// Attempts to serialize the specified packet to the destination span.
    /// </summary>
    /// <param name="packet">The packet to serialize.</param>
    /// <param name="destination">The destination span to hold the serialized packet.</param>
    /// <param name="bytesWritten">The number of bytes written to the destination span.</param>
    /// <returns>Returns true if serialization was successful; otherwise, false.</returns>
    public static bool TrySerialize(in Packet packet, Span<byte> destination, out int bytesWritten)
    {
        int totalSize = PacketSize.Header + packet.Payload.Length;

        if (packet.Payload.Length > ushort.MaxValue || destination.Length < totalSize)
        {
            bytesWritten = 0;
            return false;
        }

        try
        {
            PacketSerializer.WritePacketFast(destination[..totalSize], in packet);
            bytesWritten = totalSize;
            return true;
        }
        catch
        {
            bytesWritten = 0;
            return false;
        }
    }

    /// <summary>
    /// Attempts to deserialize the specified source span to a packet.
    /// </summary>
    /// <param name="source">The source span to deserialize.</param>
    /// <param name="packet">When this method returns, contains the deserialized packet if the operation was successful; otherwise, the default packet value.</param>
    /// <returns>Returns true if deserialization was successful; otherwise, false.</returns>
    public static bool TryDeserialize(ReadOnlySpan<byte> source, out Packet packet)
    {
        packet = default;

        if (source.Length < PacketSize.Header)
            return false;

        try
        {
            short length = MemoryMarshal.Read<short>(source);
            if (length < PacketSize.Header || length > source.Length)
                return false;

            packet = PacketSerializer.ReadPacketFast(source[..length]);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Converts the specified packet to a readable string representation.
    /// </summary>
    /// <param name="packet">The packet to convert.</param>
    /// <returns>A string representation of the packet.</returns>
    public static string ToReadableString(in Packet packet)
        => $"Type: {packet.Type}, " +
           $"Flags: {packet.Flags}, " +
           $"Priority: {packet.Priority}, " +
           $"Id: {packet.Id}, " +
           $"Payload Length: {packet.Payload.Length}";
}
