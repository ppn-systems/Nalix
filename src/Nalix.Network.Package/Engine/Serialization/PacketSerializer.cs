using Nalix.Common.Constants;
using Nalix.Common.Package.Metadata;

namespace Nalix.Network.Package.Engine.Serialization;

/// <summary>
/// Provides high-performance methods for serializing and deserializing network packets.
/// </summary>
[System.Runtime.CompilerServices.SkipLocalsInit]
public static partial class PacketSerializer
{
    #region Constants

    // Pre-allocated buffers for stream operations
    private static readonly System.Threading.ThreadLocal<System.Byte[]> _threadLocalHeaderBuffer =
        new(() => new System.Byte[PacketSize.Header], trackAllValues: false);

    private const int Threshold = 32768;

    #endregion Constants

    #region Internal Methods

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static byte[] RentHeaderBuffer()
    {
        byte[]? buffer = _threadLocalHeaderBuffer.Value;
        if (buffer == null)
        {
            buffer = new System.Byte[PacketSize.Header];
            _threadLocalHeaderBuffer.Value = buffer;
        }

        return buffer;
    }

    /// <summary>
    /// Efficiently materializes a payload using unsafe code when appropriate.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static unsafe void MaterializePayload(
        System.ReadOnlySpan<System.Byte> data,
        int payloadSize, out System.Byte[] payload)
    {
        // For empty payloads, avoid allocation
        if (payloadSize == 0)
        {
            payload = [];
            return;
        }

        // For large payloads, allocate directly
        byte[] buffer = new System.Byte[payloadSize];

        fixed (byte* source = data)
        fixed (byte* destination = buffer)
        {
            System.Buffer.MemoryCopy(source, destination, payloadSize, payloadSize);
        }

        payload = buffer;
    }

    #endregion Internal Methods

    #region Methods Serialization

    /// <summary>
    /// Serializes the specified packet to a byte array.
    /// </summary>
    /// <param name="packet">The packet to serialize.</param>
    /// <returns>The serialized byte array representing the packet.</returns>
    public static System.Byte[] Serialize(in Packet packet)
    {
        int totalSize = PacketSize.Header + packet.Payload.Length;

        if (totalSize <= PacketConstants.StackAllocLimit)
        {
            System.Span<System.Byte> stackBuffer = stackalloc System.Byte[totalSize];
            WritePacket(stackBuffer, in packet);
            return stackBuffer.ToArray();
        }
        else
        {
            System.Byte[] rentedArray = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(totalSize);
            try
            {
                WritePacket(System.MemoryExtensions.AsSpan(rentedArray, 0, totalSize), in packet);
                return System.MemoryExtensions.AsSpan(rentedArray, 0, totalSize).ToArray();
            }
            finally
            {
                System.Buffers.ArrayPool<System.Byte>.Shared.Return(rentedArray, clearArray: true);
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
    public static Packet Deserialize(System.ReadOnlySpan<System.Byte> data) => ReadPacket(data);

    /// <summary>
    /// Deserializes the specified ReadOnlyMemory to a packet.
    /// </summary>
    /// <param name="data">The ReadOnlyMemory to deserialize.</param>
    /// <returns>The deserialized packet.</returns>
    public static Packet Deserialize(System.ReadOnlyMemory<System.Byte> data) => Deserialize(data.Span);

    /// <summary>
    /// Deserializes the specified byte array to a packet.
    /// </summary>
    /// <param name="data">The byte array to deserialize.</param>
    /// <returns>The deserialized packet.</returns>
    public static Packet Deserialize(System.Byte[] data) => Deserialize(new System.ReadOnlySpan<System.Byte>(data));

    #endregion Methods Deserialization

    #region Methods Try Serializer

    /// <summary>
    /// Attempts to serialize the specified packet to the destination span.
    /// </summary>
    /// <param name="packet">The packet to serialize.</param>
    /// <param name="destination">The destination span to hold the serialized packet.</param>
    /// <param name="bytesWritten">The Number of bytes written to the destination span.</param>
    /// <returns>Returns true if serialization was successful; otherwise, false.</returns>
    public static bool TrySerialize(
        in Packet packet, System.Span<System.Byte> destination,
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
    public static bool TryDeserialize(
        System.ReadOnlySpan<System.Byte> source,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Packet packet)
    {
        packet = default;

        if (source.Length < PacketSize.Header)
            return false;

        try
        {
            ushort length = System.Runtime.InteropServices.MemoryMarshal.Read<ushort>(source);
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
