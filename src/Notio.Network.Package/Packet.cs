using Notio.Common.Constants;
using Notio.Common.Cryptography;
using Notio.Common.Package;
using Notio.Common.Package.Enums;
using Notio.Common.Package.Metadata;
using Notio.Common.Security;
using Notio.Defaults;
using Notio.Network.Package.Extensions;
using Notio.Utilities;
using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Serialization.Metadata;

namespace Notio.Network.Package;

/// <summary>
/// Represents an immutable network packet with metadata and payload.
/// This high-performance struct is optimized for efficient serialization and transmission.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
[DebuggerDisplay("Packet {Number}: Number={Number}, Type={Type}, Number={Number}, Length={Length}")]
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

    #region Compression Methods

    /// <inheritdoc />
    static Packet IPacketCompressor<Packet>.Compress(Packet packet, CompressionMode type)
        => PacketCompression.CompressPayload(packet, type);

    /// <inheritdoc />
    static Packet IPacketCompressor<Packet>.Decompress(Packet packet, CompressionMode type)
        => PacketCompression.DecompressPayload(packet, type);

    #endregion

    #region Encryption Methods

    /// <inheritdoc />
    static Packet IPacketEncryptor<Packet>.Encrypt(Packet packet, byte[] key, EncryptionMode algorithm)
        => PacketEncryption.EncryptPayload(packet, key, algorithm);

    /// <inheritdoc />
    static Packet IPacketEncryptor<Packet>.Decrypt(Packet packet, byte[] key, EncryptionMode algorithm)
        => PacketEncryption.DecryptPayload(packet, key, algorithm);

    #endregion

    #region Cleanup Methods

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

    #region String Methods

    /// <summary>
    /// Converts the packet's data into a human-readable, detailed string representation.
    /// </summary>
    /// <remarks>
    /// This method provides a structured view of the packet's contents, including:
    /// - Number, type, flags, Number, priority, timestamp, and checksum.
    /// - Payload size and, if applicable, a hex dump of the payload data.
    /// - If the payload is larger than 32 bytes, only the first and last 16 bytes are displayed.
    /// </remarks>
    /// <returns>
    /// A formatted string containing detailed packet information.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string ToDetailedString()
    {
        StringBuilder sb = new();
        sb.AppendLine($"Packet [{Id}]:");
        sb.AppendLine($"  Code: {Code}");
        sb.AppendLine($"  Type: {Type}");
        sb.AppendLine($"  Flags: {Flags}");
        sb.AppendLine($"  Number: 0x{Number:X4}");
        sb.AppendLine($"  Priority: {Priority}");
        sb.AppendLine($"  Timestamp: {Timestamp}");
        sb.AppendLine($"  Checksum: 0x{Checksum:X8} (Valid: {IsValid()})");
        sb.AppendLine($"  Payload: {Payload.Length} bytes");

        if (Payload.Length > 0)
        {
            sb.Append("  Data: ");

            if (Payload.Length <= 32)
                for (int i = 0; i < Payload.Length; i++)
                    sb.Append($"{Payload.Span[i]:X2} ");
            else
            {
                for (int i = 0; i < 16; i++)
                    sb.Append($"{Payload.Span[i]:X2} ");

                sb.Append("... ");

                for (int i = Payload.Length - 16; i < Payload.Length; i++)
                    sb.Append($"{Payload.Span[i]:X2} ");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Gets a string representation of this packet for debugging purposes.
    /// </summary>
    /// <returns>A string that represents this packet.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString()
        => $"Packet Number={Number}, Type={Type}, Number={Id}, " +
           $"Flags={Flags}, Priority={Priority}, Timestamp={Timestamp}, " +
           $"Checksum={IsValid()}, Payload={Payload.Length} bytes, Size={Length} bytes";

    #endregion
}
