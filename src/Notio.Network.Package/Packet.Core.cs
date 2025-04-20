using Notio.Common.Constants;
using Notio.Common.Package;
using Notio.Common.Package.Enums;
using Notio.Common.Package.Metadata;
using Notio.Defaults;
using Notio.Utilities;
using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization.Metadata;

namespace Notio.Network.Package;

/// <summary>
/// Represents an immutable network packet with metadata and payload.
/// This high-performance struct is optimized for efficient serialization and transmission.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly partial struct Packet : IDisposable, IEquatable<Packet>,
    IPacket,
    IPacketEncryptor<Packet>,
    IPacketCompressor<Packet>,
    IPacketDeserializer<Packet>
{
    #region Constants

    // Cache the max packet size locally to avoid field access costs
    private const int MaxPacketSize = PacketConstants.PacketSizeLimit;
    private const int MaxHeapAllocSize = DefaultConstants.HeapAllocThreshold;
    private const int MaxStackAllocSize = DefaultConstants.StackAllocThreshold;

    #endregion

    #region Fields

    private readonly ulong _hash;

    #endregion

    #region Properties

    /// <summary>
    /// Gets the total length of the packet including header and payload.
    /// </summary>
    public ushort Length => (ushort)(PacketSize.Header + Payload.Length);

    /// <summary>
    /// Gets the Number associated with the packet, which specifies an operation type.
    /// </summary>
    public ushort Id { get; }

    /// <summary>
    /// Gets the CRC32 checksum of the packet payload for integrity validation.
    /// </summary>
    public uint Checksum { get; }

    /// <summary>
    /// Gets the timestamp when the packet was created in microseconds since system startup.
    /// </summary>
    public ulong Timestamp { get; }

    /// <summary>
    /// Gets the packet Hash.
    /// </summary>
    public ulong Hash => _hash;

    /// <summary>
    /// Gets the packet code, which is used to identify the packet type.
    /// </summary>
    public PacketCode Code { get; }

    /// <summary>
    /// Gets the packet type, which specifies the kind of packet.
    /// </summary>
    public PacketType Type { get; }

    /// <summary>
    /// Gets the flags associated with the packet, used for additional control information.
    /// </summary>
    public PacketFlags Flags { get; }

    /// <summary>
    /// Gets the priority level of the packet, which affects how the packet is processed.
    /// </summary>
    public PacketPriority Priority { get; }

    /// <summary>
    /// Gets the packet identifier, which is a unique identifier for this packet instance.
    /// </summary>
    public byte Number { get; }

    /// <summary>
    /// Gets the payload data being transmitted in this packet.
    /// </summary>
    public Memory<byte> Payload { get; }

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="Packet"/> struct with a specific Number and payload.
    /// </summary>
    /// <param name="id">The packet Number.</param>
    /// <param name="code">The packet code.</param>
    /// <param name="payload">The packet payload (data).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Packet(ushort id, PacketCode code, byte[] payload)
    : this(id, code, new Memory<byte>(payload))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Packet"/> struct with a specific Number and payload.
    /// </summary>
    /// <param name="id">The packet Number.</param>
    /// <param name="code">The packet code.</param>
    /// <param name="payload">The packet payload (data).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Packet(ushort id, PacketCode code, Span<byte> payload)
    : this(id, code, new Memory<byte>(payload.ToArray()))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Packet"/> struct with a specific Number and payload.
    /// </summary>
    /// <param name="id">The packet Number.</param>
    /// <param name="code">The packet code.</param>
    /// <param name="payload">The packet payload (data).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Packet(ushort id, PacketCode code, Memory<byte> payload)
        : this(id, code, PacketType.None, PacketFlags.None, PacketPriority.None, payload)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Packet"/> struct with specified enum values for flags and priority.
    /// </summary>
    /// <param name="id">The packet id.</param>
    /// <param name="code">The packet code.</param>
    /// <param name="flags">The packet flags.</param>
    /// <param name="priority">The packet priority.</param>
    /// <param name="s">The packet payload as a UTF-8 encoded string.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Packet(ushort id, PacketCode code, PacketFlags flags, PacketPriority priority, string s)
        : this(id, code, PacketType.String, flags, priority, DefaultConstants.DefaultEncoding.GetBytes(s))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Packet"/> struct with the specified flags, priority, id, and payload.
    /// </summary>
    /// <param name="id">The identifier for the packet.</param>
    /// <param name="code">The packet code.</param>
    /// <param name="flags">The packet flags indicating specific properties of the packet.</param>
    /// <param name="priority">The priority level of the packet.</param>
    /// <param name="obj">The payload of the packet.</param>
    /// <param name="jsonTypeInfo">The metadata used for JSON serialization.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Packet(ushort id, PacketCode code, PacketFlags flags,
        PacketPriority priority, object obj, JsonTypeInfo<object> jsonTypeInfo)
        : this(id, code, PacketType.Object,
               flags, priority, JsonBuffer.SerializeToMemory(obj, jsonTypeInfo))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Packet"/> struct with type, flags, priority, id, and payload.
    /// </summary>
    /// <param name="id">The packet id.</param>
    /// <param name="code">The packet code.</param>
    /// <param name="type">The packet type.</param>
    /// <param name="flags">The packet flags.</param>
    /// <param name="priority">The packet priority.</param>
    /// <param name="payload">The packet payload (data).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Packet(ushort id, ushort code, byte type, byte flags, byte priority, Memory<byte> payload)
        : this(id, (PacketCode)code, (PacketType)type, (PacketFlags)flags, (PacketPriority)priority, payload)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Packet"/> struct with specified enum values for type, flags, and priority.
    /// </summary>
    /// <param name="id">The packet id.</param>
    /// <param name="code">The packet code.</param>
    /// <param name="type">The packet type.</param>
    /// <param name="flags">The packet flags.</param>
    /// <param name="priority">The packet priority.</param>
    /// <param name="payload">The packet payload (data).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Packet(ushort id, PacketCode code, PacketType type, PacketFlags flags, PacketPriority priority, Memory<byte> payload)
        : this(id, 0, MicrosecondClock.GetTimestamp(), code, type, flags, priority, 0, payload, true)
    {
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Releases any resources used by this packet, returning rented arrays to the pool.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        // Only return large arrays to the pool
        if (Payload.Length > MaxHeapAllocSize &&
            MemoryMarshal.TryGetArray<byte>(Payload, out var segment) &&
            segment.Array is { } array)
        {
            ArrayPool<byte>.Shared.Return(array, clearArray: true);
        }
    }

    #endregion
}
