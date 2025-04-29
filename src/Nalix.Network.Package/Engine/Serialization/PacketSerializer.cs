using Nalix.Common.Constants;
using Nalix.Common.Package.Metadata;
using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Nalix.Network.Package.Engine.Serialization;

/// <summary>
/// Provides high-performance methods for serializing and deserializing network packets.
/// </summary>
[SkipLocalsInit]
public static partial class PacketSerializer
{
    #region Constants

    // Pre-allocated buffers for stream operations
    private static readonly ThreadLocal<byte[]> _threadLocalHeaderBuffer =
        new(() => new byte[PacketSize.Header], trackAllValues: false);

    private const int Threshold = 32768;

    #endregion Constants

    #region Internal Methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static byte[] RentHeaderBuffer()
    {
        byte[]? buffer = _threadLocalHeaderBuffer.Value;
        if (buffer == null)
        {
            buffer = new byte[PacketSize.Header];
            _threadLocalHeaderBuffer.Value = buffer;
        }

        return buffer;
    }

    /// <summary>
    /// Efficiently materializes a payload using unsafe code when appropriate.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static unsafe void MaterializePayload(ReadOnlySpan<byte> data, int payloadSize, out Memory<byte> payload)
    {
        // For empty payloads, avoid allocation
        if (payloadSize == 0)
        {
            payload = Memory<byte>.Empty;
            return;
        }

        // For small payloads, use a pooled buffer
        if (payloadSize <= 4096)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(payloadSize);

            // Fast copy using unsafe pointer arithmetic for small-to-medium payloads
            fixed (byte* source = data)
            fixed (byte* destination = buffer)
            {
                Buffer.MemoryCopy(source, destination, payloadSize, payloadSize);
            }

            // Note: The caller is responsible for returning this buffer to the pool
            payload = buffer.AsMemory(0, payloadSize);
        }
        else
        {
            // For large payloads, allocate directly
            byte[] buffer = new byte[payloadSize];

            fixed (byte* source = data)
            fixed (byte* destination = buffer)
            {
                Buffer.MemoryCopy(source, destination, payloadSize, payloadSize);
            }

            payload = buffer;
        }
    }

    #endregion Internal Methods

    #region Methods Serialization

    /// <summary>
    /// Serializes the specified packet to a byte array.
    /// </summary>
    /// <param name="packet">The packet to serialize.</param>
    /// <returns>The serialized byte array representing the packet.</returns>
    public static byte[] Serialize(in Packet packet)
    {
        int totalSize = PacketSize.Header + packet.Payload.Length;

        if (totalSize <= PacketConstants.StackAllocLimit)
        {
            Span<byte> stackBuffer = stackalloc byte[totalSize];
            WritePacket(stackBuffer, in packet);
            return stackBuffer.ToArray();
        }
        else
        {
            byte[] rentedArray = PacketConstants.SharedBytePool.Rent(totalSize);
            try
            {
                WritePacket(rentedArray.AsSpan(0, totalSize), in packet);
                return rentedArray.AsSpan(0, totalSize).ToArray();
            }
            finally
            {
                PacketConstants.SharedBytePool.Return(rentedArray, clearArray: true);
            }
        }
    }

    #endregion Methods Serialization

    #region Methods Deserialization

    /// <summary>
    /// Deserializes the specified byte array to a packet.
    /// </summary>
    /// <param name="data">The byte array to deserialize.</param>
    /// <returns>The deserialized packet.</returns>
    public static Packet Deserialize(ReadOnlySpan<byte> data) => ReadPacket(data);

    /// <summary>
    /// Deserializes the specified ReadOnlyMemory to a packet.
    /// </summary>
    /// <param name="data">The ReadOnlyMemory to deserialize.</param>
    /// <returns>The deserialized packet.</returns>
    public static Packet Deserialize(ReadOnlyMemory<byte> data) => Deserialize(data.Span);

    /// <summary>
    /// Deserializes the specified byte array to a packet.
    /// </summary>
    /// <param name="data">The byte array to deserialize.</param>
    /// <returns>The deserialized packet.</returns>
    public static Packet Deserialize(byte[] data) => Deserialize(new ReadOnlySpan<byte>(data));

    #endregion Methods Deserialization

    #region Methods Try Serializer

    /// <summary>
    /// Attempts to serialize the specified packet to the destination span.
    /// </summary>
    /// <param name="packet">The packet to serialize.</param>
    /// <param name="destination">The destination span to hold the serialized packet.</param>
    /// <param name="bytesWritten">The Number of bytes written to the destination span.</param>
    /// <returns>Returns true if serialization was successful; otherwise, false.</returns>
    public static bool TrySerialize(in Packet packet, Span<byte> destination,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out int bytesWritten)
    {
        int totalSize = PacketSize.Header + packet.Payload.Length;

        if (packet.Payload.Length > ushort.MaxValue || destination.Length < totalSize)
        {
            bytesWritten = 0;
            return false;
        }

        try
        {
            WritePacket(destination[..totalSize], in packet);
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
    public static bool TryDeserialize(ReadOnlySpan<byte> source, [NotNullWhen(true)] out Packet packet)
    {
        packet = default;

        if (source.Length < PacketSize.Header)
            return false;

        try
        {
            short length = MemoryMarshal.Read<short>(source);
            if (length < PacketSize.Header || length > source.Length)
                return false;

            packet = ReadPacket(source[..length]);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion Methods Try Serializer
}
