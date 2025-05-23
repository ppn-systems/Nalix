using Nalix.Common.Package;
using Nalix.Common.Package.Enums;
using Nalix.Common.Serialization;
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
    /// Creates an empty packet with the specified id and code.
    /// </summary>
    /// <param name="id">The packet Number.</param>
    public static Packet Empty(ushort id)
        => new(id, PacketType.None, PacketFlags.None, PacketPriority.Low, System.Memory<byte>.Empty);

    /// <summary>
    /// Initializes a new instance of the <see cref="Packet"/> struct with a specific Number and payload.
    /// </summary>
    /// <param name="id">The packet Number.</param>
    /// <param name="payload">The packet payload (data).</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Packet(ushort id, byte[] payload)
        : this(id, new System.Memory<byte>(payload))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Packet"/> struct with a specific Number and payload.
    /// </summary>
    /// <param name="id">The packet Number.</param>
    /// <param name="payload">The packet payload (data).</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Packet(ushort id, ISerializable payload)
    {
        int size = payload.GetSize();
        _rentedBuffer = System.Buffers.ArrayPool<byte>.Shared.Rent(size);

        payload.Serialize(
            System.MemoryExtensions.AsSpan(_rentedBuffer, 0, size),
            out int written);

        System.Memory<byte> memory = new(_rentedBuffer, 0, written);

        this = new Packet(
            id: id,
            number: 0,
            checksum: 0,
            timestamp: 0,
            type: PacketType.Object,
            flags: PacketFlags.None,
            priority: PacketPriority.Low,
            payload: memory
        );
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Packet"/> struct with a specific Number and payload.
    /// </summary>
    /// <param name="id">The packet Number.</param>
    /// <param name="payload">The packet payload (data).</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Packet(ushort id, System.ReadOnlySpan<byte> payload)
        : this(id, new System.Memory<byte>(payload.ToArray()))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Packet"/> struct with a specific Number and payload.
    /// </summary>
    /// <param name="id">The packet Number.</param>
    /// <param name="payload">The packet payload (data).</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Packet(ushort id, System.Memory<byte> payload)
        : this(id, PacketType.Binary, PacketFlags.None, PacketPriority.Low, payload)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Packet"/> struct with specified enum values for flags and priority.
    /// </summary>
    /// <param name="id">The packet id.</param>
    /// <param name="s">The packet payload as a UTF-8 encoded string.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Packet(ushort id, string s)
        : this(id, PacketType.String, PacketFlags.None, PacketPriority.Low, SerializationOptions.Encoding.GetBytes(s))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Packet"/> struct with type, flags, priority, id, and payload.
    /// </summary>
    /// <param name="id">The packet id.</param>
    /// <param name="type">The packet type.</param>
    /// <param name="flags">The packet flags.</param>
    /// <param name="priority">The packet priority.</param>
    /// <param name="payload">The packet payload (data).</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Packet(
        ushort id,
        byte type,
        byte flags,
        byte priority,
        System.Memory<byte> payload)
        : this(id, (PacketType)type, (PacketFlags)flags, (PacketPriority)priority, payload)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Packet"/> struct with specified enum values for type, flags, and priority.
    /// </summary>
    /// <param name="id">The packet id.</param>
    /// <param name="type">The packet type.</param>
    /// <param name="flags">The packet flags.</param>
    /// <param name="priority">The packet priority.</param>
    /// <param name="payload">The packet payload (data).</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Packet(
        ushort id,
        PacketType type,
        PacketFlags flags,
        PacketPriority priority,
        System.Memory<byte> payload)
        : this(id, 0, 0, 0, type, flags, priority, payload)
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
