using Nalix.Common.Package;
using Nalix.Common.Package.Enums;
using Nalix.Environment;

namespace Nalix.Network.Package;

/// <summary>
/// Represents an immutable network packet with metadata and payload.
/// This high-performance struct is optimized for efficient serialization and transmission.
/// </summary>
[System.Runtime.InteropServices.StructLayout(
    System.Runtime.InteropServices.LayoutKind.Sequential)]
public readonly partial struct Packet : IPacket, System.IDisposable
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="Packet"/> struct with a specific Number and payload.
    /// </summary>
    /// <param name="opCode">The packet Number.</param>
    /// <param name="type">The packet type.</param>
    /// <param name="flags">The packet flags.</param>
    /// <param name="priority">The packet priority.</param>
    /// <param name="payload">The packet payload (data).</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Packet(
        System.UInt16 opCode,
        PacketType type,
        PacketFlags flags,
        PacketPriority priority,
        System.Byte[] payload)
    {
        System.Int32 length = payload.Length;
        _rentedBuffer = System.Buffers.ArrayPool<byte>.Shared.Rent(length);

        // Unsafe copy from managed array to rented buffer

        unsafe
        {
            fixed (byte* src = payload)
            fixed (byte* dst = _rentedBuffer)
            {
                System.Runtime.CompilerServices.Unsafe
                    .CopyBlockUnaligned(dst, src, (System.UInt32)length);
            }
        }

        this = new Packet(
            opCode: opCode,
            number: 0,
            checksum: 0,
            timestamp: 0,
            type: type,
            flags: flags,
            priority: priority,
            payload: new System.Memory<byte>(_rentedBuffer, 0, length)
        );
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Packet"/> struct with a specific Number and payload.
    /// </summary>
    /// <param name="opCode">The packet Number.</param>
    /// <param name="payload">The packet payload (data).</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Packet(ushort opCode, System.ReadOnlySpan<byte> payload)
        : this(opCode, new System.Memory<byte>(payload.ToArray()))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Packet"/> struct with a specific Number and payload.
    /// </summary>
    /// <param name="opCode">The packet Number.</param>
    /// <param name="payload">The packet payload (data).</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Packet(ushort opCode, System.Memory<byte> payload)
        : this(opCode, PacketType.Binary, PacketFlags.None, PacketPriority.Low, payload)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Packet"/> struct with specified enum values for flags and priority.
    /// </summary>
    /// <param name="opCode">The packet opCode.</param>
    /// <param name="s">The packet payload as a UTF-8 encoded string.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Packet(ushort opCode, string s)
        : this(opCode, PacketType.String, PacketFlags.None, PacketPriority.Low, SerializationOptions.Encoding.GetBytes(s))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Packet"/> struct with type, flags, priority, opCode, and payload.
    /// </summary>
    /// <param name="opCode">The packet opCode.</param>
    /// <param name="type">The packet type.</param>
    /// <param name="flags">The packet flags.</param>
    /// <param name="priority">The packet priority.</param>
    /// <param name="payload">The packet payload (data).</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Packet(
        ushort opCode,
        byte type,
        byte flags,
        byte priority,
        System.Memory<byte> payload)
        : this(opCode, (PacketType)type, (PacketFlags)flags, (PacketPriority)priority, payload)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Packet"/> struct with specified enum values for type, flags, and priority.
    /// </summary>
    /// <param name="opCode">The packet opCode.</param>
    /// <param name="type">The packet type.</param>
    /// <param name="flags">The packet flags.</param>
    /// <param name="priority">The packet priority.</param>
    /// <param name="payload">The packet payload (data).</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Packet(
        ushort opCode,
        PacketType type,
        PacketFlags flags,
        PacketPriority priority,
        System.Memory<byte> payload)
        : this(opCode, 0, 0, 0, type, flags, priority, payload)
    {
    }

    #endregion Constructors

    #region IDisposable

    /// <summary>
    /// Releases any resources used by this packet, returning rented arrays to the pool.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        // Only return large arrays to the pool
        if (Payload.Length > MaxHeapAllocSize &&
            System.Runtime.InteropServices.MemoryMarshal.TryGetArray<byte>
            (Payload, out System.ArraySegment<byte> segment) &&
            segment.Array is { } array)
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(array, clearArray: true);
        }

        if (_rentedBuffer != null)
            System.Buffers.ArrayPool<byte>.Shared.Return(_rentedBuffer, clearArray: true);
    }

    #endregion IDisposable
}
