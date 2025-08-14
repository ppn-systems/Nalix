// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Attributes;
using Nalix.Common.Connection.Protocols;
using Nalix.Common.Packets;
using Nalix.Common.Packets.Enums;
using Nalix.Common.Packets.Interfaces;
using Nalix.Common.Security.Cryptography.Enums;
using Nalix.Common.Serialization;
using Nalix.Common.Serialization.Attributes;
using Nalix.Shared.Memory.Pooling;
using Nalix.Shared.Serialization;

namespace Nalix.Shared.Messaging;

/// <summary>
/// Represents a binary data packet used for transmitting raw bytes over the network.
/// </summary>
[MagicNumber(MagicNumbers.BinaryPacket)]
[SerializePackable(SerializeLayout.Explicit)]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.Diagnostics.DebuggerDisplay("BinaryPacket<T> OpCode={OpCode}, Length={Length}, Flags={Flags}")]
public sealed class BinaryPacket<T> : IPacket, IPacketTransformer<BinaryPacket<T>>
    where T : IFixedSizeSerializable
{
    /// <inheritdoc/>
    public const System.Int32 DynamicSize = 1024;

    /// <inheritdoc/>
    public static System.Int32 Size => PacketConstants.HeaderSize + DynamicSize;

    /// <summary>
    /// Gets the total length of the serialized packet in bytes, including header and content.
    /// </summary>
    [SerializeIgnore]
    public System.UInt16 Length =>
        (System.UInt16)(PacketConstants.HeaderSize + (Data is null ? 0 : T.Size));

    /// <summary>
    /// Gets the magic number used to identify the packet format.
    /// </summary>
    [SerializeOrder(0)]
    public System.UInt32 MagicNumber { get; set; }

    /// <summary>
    /// Gets the operation code (OpCode) of this packet.
    /// </summary>
    [SerializeOrder(4)]
    public System.UInt16 OpCode { get; set; }

    /// <summary>
    /// Gets the flags associated with this packet.
    /// </summary>
    [SerializeOrder(6)]
    public PacketFlags Flags { get; set; }

    /// <summary>
    /// Gets the packet priority.
    /// </summary>
    [SerializeOrder(7)]
    public PacketPriority Priority { get; set; }

    /// <summary>
    /// Gets the transport protocol (e.g., TCP/UDP) this packet targets.
    /// </summary>
    [SerializeOrder(8)]
    public TransportProtocol Transport { get; set; }

    /// <summary>
    /// Gets or sets the binary content of the packet.
    /// </summary>
    [SerializeOrder(9)]
    [SerializeDynamicSize(DynamicSize)]
    public T Data { get; set; }

    /// <summary>
    /// Initializes a new <see cref="BinaryPacket"/> with empty content.
    /// </summary>
    public BinaryPacket()
    {
        this.Data = CreateNonNull();
        this.Flags = PacketFlags.None;
        this.Priority = PacketPriority.Normal;
        this.Transport = TransportProtocol.Null;
        this.OpCode = PacketConstants.OpCodeDefault;
        this.MagicNumber = (System.UInt32)MagicNumbers.BinaryPacket;
    }

    /// <summary>
    /// Initializes the packet with binary data.
    /// </summary>
    /// <param name="data">Binary content of the packet.</param>
    public void Initialize(T data) => Initialize(data, TransportProtocol.Null);

    /// <summary>
    /// Initializes the packet with binary data and a transport protocol.
    /// </summary>
    /// <param name="data">Binary content of the packet.</param>
    /// <param name="transport">The target transport protocol.</param>
    public void Initialize(T data, TransportProtocol transport = TransportProtocol.Tcp)
    {
        this.Transport = transport;
        this.Data = System.Collections.Generic.EqualityComparer<T>.Default.Equals(data, default!) ? CreateNonNull() : data;
    }

    /// <summary>
    /// Serializes the packet to a newly allocated byte array.
    /// </summary>
    public System.Byte[] Serialize() => LiteSerializer.Serialize(this);

    /// <summary>
    /// Serializes the packet into the provided destination buffer.
    /// </summary>
    /// <param name="buffer">The destination buffer. Must be large enough.</param>
    public void Serialize(System.Span<System.Byte> buffer) => LiteSerializer.Serialize(this, buffer);

    /// <summary>
    /// Deserializes a <see cref="BinaryPacket"/> from the specified buffer.
    /// </summary>
    /// <param name="buffer">The source buffer.</param>
    /// <returns>A pooled <see cref="BinaryPacket"/> instance.</returns>
    public static BinaryPacket<T> Deserialize(in System.ReadOnlySpan<System.Byte> buffer)
    {
        BinaryPacket<T> packet = ObjectPoolManager.Instance.Get<BinaryPacket<T>>();
        System.Int32 bytesRead = LiteSerializer.Deserialize(buffer, ref packet);

        return bytesRead == 0
            ? throw new System.InvalidOperationException(
                "Failed to deserialize packet: No bytes were read.")
            : packet;
    }

    /// <summary>
    /// Encrypts the packet content.
    /// </summary>
    /// <remarks><b>Internal infrastructure API. Do not call directly.</b></remarks>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    [System.Obsolete("Internal infrastructure API. Encryption is handled by the pipeline.", error: true)]
    public static BinaryPacket<T> Encrypt(BinaryPacket<T> packet, System.Byte[] key, SymmetricAlgorithmType algorithm)
        => throw new System.NotImplementedException();

    /// <summary>
    /// Decrypts the packet content.
    /// </summary>
    /// <remarks><b>Internal infrastructure API. Do not call directly.</b></remarks>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    [System.Obsolete("Internal infrastructure API. Decryption is handled by the pipeline.", error: true)]
    public static BinaryPacket<T> Decrypt(BinaryPacket<T> packet, System.Byte[] key, SymmetricAlgorithmType algorithm)
        => throw new System.NotImplementedException();

    /// <summary>
    /// Compresses <see cref="Data"/> using LZ4 (raw bytes, no Base64).
    /// </summary>
    /// <remarks><b>Internal infrastructure API. Do not call directly.</b></remarks>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static BinaryPacket<T> Compress(BinaryPacket<T> packet)
        => throw new System.NotImplementedException();

    /// <summary>
    /// Decompresses <see cref="Data"/> previously compressed with LZ4.
    /// </summary>
    /// <remarks><b>Internal infrastructure API. Do not call directly.</b></remarks>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static BinaryPacket<T> Decompress(BinaryPacket<T> packet)
        => throw new System.NotImplementedException();

    /// <summary>
    /// Resets this instance to its default state for pooling reuse.
    /// </summary>
    public void ResetForPool()
    {
        this.Data = CreateNonNull();
        this.Flags = PacketFlags.None;
        this.Priority = PacketPriority.Normal;
        this.Transport = TransportProtocol.Null;
    }

    /// <inheritdoc/>
    public override System.String ToString() =>
        $"BinaryPacket(OpCode={OpCode}, Length={Length}, Flags={Flags}, " +
        $"Priority={Priority}, Transport={Transport}, Data={T.Size} bytes)";

    private static T CreateNonNull()
    {
        if (typeof(T) == typeof(System.Byte[]))
        {
            return (T)(System.Object)System.Array.Empty<System.Byte>();
        }

        try
        {
            return System.Activator.CreateInstance<T>(); // struct -> ok, class có ctor mặc định -> ok
        }
        catch
        {
            return default!; // class không có ctor mặc định -> đành chấp nhận default
        }
    }
}
