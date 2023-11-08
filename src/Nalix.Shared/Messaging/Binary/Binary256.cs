// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Attributes;
using Nalix.Common.Connection.Protocols;
using Nalix.Common.Packets;
using Nalix.Common.Packets.Enums;
using Nalix.Common.Packets.Interfaces;
using Nalix.Common.Security.Cryptography.Enums;
using Nalix.Common.Serialization;
using Nalix.Common.Serialization.Attributes;
using Nalix.Shared.Extensions;
using Nalix.Shared.LZ4;
using Nalix.Shared.Memory.Pooling;
using Nalix.Shared.Serialization;

namespace Nalix.Shared.Messaging.Binary;

/// <summary>
/// Represents a binary data packet used for transmitting raw bytes over the network.
/// </summary>
[MagicNumber(MagicNumbers.Binary256)]
[SerializePackable(SerializeLayout.Explicit)]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.Diagnostics.DebuggerDisplay("Binary128 OpCode={OpCode}, Length={Length}, Flags={Flags}")]
public class Binary256 : IPacket, IPacketTransformer<Binary256>
{
    /// <inheritdoc/>
    public const System.Int32 DynamicSize = 256;

    /// <inheritdoc/>
    public static System.Int32 Size => PacketConstants.HeaderSize + DynamicSize;

    /// <summary>
    /// Gets the total length of the serialized packet in bytes, including header and content.
    /// </summary>
    [SerializeIgnore]
    public System.UInt16 Length =>
        (System.UInt16)(PacketConstants.HeaderSize + (Data?.Length ?? 0));

    /// <summary>
    /// Gets the magic number used to identify the packet format.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.MagicNumber)]
    public System.UInt32 MagicNumber { get; set; }

    /// <summary>
    /// Gets the operation code (OpCode) of this packet.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.OpCode)]
    public System.UInt16 OpCode { get; set; }

    /// <summary>
    /// Gets the flags associated with this packet.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.Flags)]
    public PacketFlags Flags { get; set; }

    /// <summary>
    /// Gets the packet priority.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.Priority)]
    public PacketPriority Priority { get; set; }

    /// <summary>
    /// Gets the transport protocol (e.g., TCP/UDP) this packet targets.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.Transport)]
    public TransportProtocol Transport { get; set; }

    /// <summary>
    /// Gets or sets the binary content of the packet.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.End)]
    [SerializeDynamicSize(DynamicSize)]
    public System.Byte[] Data { get; set; }

    /// <summary>
    /// Initializes a new <see cref="Binary128"/> with empty content.
    /// </summary>
    public Binary256()
    {
        Data = [];
        Flags = PacketFlags.None;
        Priority = PacketPriority.Normal;
        Transport = TransportProtocol.Null;
        OpCode = PacketConstants.OpCodeDefault;
        MagicNumber = (System.UInt32)MagicNumbers.Binary128;
    }

    /// <summary>
    /// Initializes the packet with binary data and a transport protocol.
    /// </summary>
    /// <param name="data">Binary content of the packet.</param>
    /// <param name="transport">The target transport protocol.</param>
    public void Initialize(
        System.Byte[] data,
        TransportProtocol transport = TransportProtocol.Tcp)
    {
        if (data.Length > DynamicSize)
        {
            throw new System.ArgumentOutOfRangeException(nameof(data), $"Binary supports at most {DynamicSize} bytes.");
        }

        this.Data = data ?? [];
        this.Transport = transport;
    }

    /// <summary>
    /// Serializes the packet to a newly allocated byte array.
    /// </summary>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Byte[] Serialize() => LiteSerializer.Serialize(this);

    /// <summary>
    /// Serializes the packet into the provided destination buffer.
    /// </summary>
    /// <param name="buffer">The destination buffer. Must be large enough.</param>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Serialize(System.Span<System.Byte> buffer) => LiteSerializer.Serialize(this, buffer);

    /// <summary>
    /// Deserializes a <see cref="Binary128"/> from the specified buffer.
    /// </summary>
    /// <param name="buffer">The source buffer.</param>
    /// <returns>A pooled <see cref="Binary128"/> instance.</returns>
    public static Binary256 Deserialize(in System.ReadOnlySpan<System.Byte> buffer)
    {
        Binary256 packet = ObjectPoolManager.Instance.Get<Binary256>();
        System.Int32 bytesRead = LiteSerializer.Deserialize(buffer, ref packet);

        return bytesRead == 0
            ? throw new System.InvalidOperationException(
                "Failed to deserialize packet: No bytes were read.")
            : packet;
    }

    /// <remarks><b>Internal infrastructure API. Do not call directly.</b></remarks>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    [System.Obsolete("Internal infrastructure API. Encryption is handled by the pipeline.", error: true)]
    public static Binary256 Encrypt(Binary256 packet, System.Byte[] key, SymmetricAlgorithmType algorithm)
        => throw new System.NotImplementedException();

    /// <remarks><b>Internal infrastructure API. Do not call directly.</b></remarks>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    [System.Obsolete("Internal infrastructure API. Decryption is handled by the pipeline.", error: true)]
    public static Binary256 Decrypt(Binary256 packet, System.Byte[] key, SymmetricAlgorithmType algorithm)
        => throw new System.NotImplementedException();

    /// <summary>
    /// Compresses <see cref="Data"/> using LZ4 (raw bytes, no Base64).
    /// </summary>
    /// <remarks><b>Internal infrastructure API. Do not call directly.</b></remarks>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static Binary256 Compress(Binary256 packet)
    {
        System.ArgumentNullException.ThrowIfNull(packet);

        if (packet.Data is null || packet.Data.Length == 0)
        {
            return packet;
        }

        System.Byte[] compressed = LZ4Codec.Encode(packet.Data);
        packet.Data = compressed;

        packet.Flags = packet.Flags.AddFlag(PacketFlags.Compressed);

        return packet;
    }

    /// <summary>
    /// Decompresses <see cref="Data"/> previously compressed with LZ4.
    /// </summary>
    /// <remarks><b>Internal infrastructure API. Do not call directly.</b></remarks>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static Binary256 Decompress(Binary256 packet)
    {
        System.ArgumentNullException.ThrowIfNull(packet);

        if (packet.Data is null || packet.Data.Length == 0)
        {
            return packet;
        }

        if (!LZ4Codec.Decode(packet.Data, out System.Byte[]? output, out System.Int32 written) ||
            output is null || written <= 0)
        {
            throw new System.InvalidOperationException("LZ4 decompression failed.");
        }

        if (written != output.Length)
        {
            // Trim to actual length written.
            System.Byte[] exact = new System.Byte[written];
            System.Buffer.BlockCopy(output, 0, exact, 0, written);
            packet.Data = exact;
        }
        else
        {
            packet.Data = output;
        }

        packet.Flags = packet.Flags.RemoveFlag(PacketFlags.Compressed);

        return packet;
    }

    /// <summary>
    /// Resets this instance to its default state for pooling reuse.
    /// </summary>
    public void ResetForPool()
    {
        this.Data = [];
        this.Flags = PacketFlags.None;
        this.Priority = PacketPriority.Normal;
        this.Transport = TransportProtocol.Null;
    }

    /// <inheritdoc/>
    public override System.String ToString() =>
        $"Binary128(OpCode={OpCode}, Length={Length}, Flags={Flags}, " +
        $"Priority={Priority}, Transport={Transport}, Data={Data?.Length ?? 0} bytes)";
}
