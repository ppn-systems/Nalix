// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Attributes;
using Nalix.Common.Caching;
using Nalix.Common.Packets;
using Nalix.Common.Packets.Abstractions;
using Nalix.Common.Packets.Enums;
using Nalix.Common.Protocols;
using Nalix.Common.Serialization;
using Nalix.Common.Serialization.Attributes;
using Nalix.Framework.Injection;
using Nalix.Shared.Extensions;
using Nalix.Shared.LZ4;
using Nalix.Shared.Memory.Pooling;
using Nalix.Shared.Serialization;

namespace Nalix.Shared.Messaging.Binary;

/// <summary>
/// Represents a binary data packet used for transmitting raw bytes over the network.
/// </summary>
[MagicNumber(FrameMagicCode.BINARY1024)]
[SerializePackable(SerializeLayout.Explicit)]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.Diagnostics.DebuggerDisplay("BINARY1024 OpCode={OpCode}, Length={Length}, Flags={Flags}")]
public class Binary1024 : FrameBase, IPoolable, IPacketDeserializer<Binary1024>, IPacketCompressor<Binary1024>
{
    /// <inheritdoc/>
    public const System.Int32 DynamicSize = 1024;

    /// <summary>
    /// Gets the total length of the serialized packet in bytes, including header and content.
    /// </summary>
    [SerializeIgnore]
    public override System.UInt16 Length =>
        (System.UInt16)(PacketConstants.HeaderSize + (Data?.Length ?? 0));

    /// <summary>
    /// Gets or sets the binary content of the packet.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.DataRegion)]
    [SerializeDynamicSize(DynamicSize)]
    public System.Byte[] Data { get; set; }

    /// <summary>
    /// Initializes a new <see cref="Binary1024"/> with empty content.
    /// </summary>
    public Binary1024()
    {
        Data = [];
        Flags = PacketFlags.None;
        Priority = PacketPriority.Normal;
        Transport = ProtocolType.NONE;
        OpCode = PacketConstants.OpCodeDefault;
        MagicNumber = (System.UInt32)FrameMagicCode.BINARY1024;
    }

    /// <summary>
    /// Initializes the packet with binary data and a transport protocol.
    /// </summary>
    /// <param name="data">Binary content of the packet.</param>
    /// <param name="transport">The target transport protocol.</param>
    public void Initialize(
        System.Byte[] data,
        ProtocolType transport = ProtocolType.TCP)
    {
        if (data.Length > DynamicSize)
        {
            throw new System.ArgumentOutOfRangeException(nameof(data), $"Binary supports at most {DynamicSize} bytes.");
        }

        this.Data = data ?? [];
        this.Transport = transport;
    }

    /// <summary>
    /// Deserializes a <see cref="Binary1024"/> from the specified buffer.
    /// </summary>
    /// <param name="buffer">The source buffer.</param>
    /// <returns>A pooled <see cref="Binary1024"/> instance.</returns>
    public static Binary1024 Deserialize(System.ReadOnlySpan<System.Byte> buffer)
    {
        Binary1024 packet = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                    .Get<Binary1024>();
        System.Int32 bytesRead = LiteSerializer.Deserialize(buffer, ref packet);

        return bytesRead == 0
            ? throw new System.InvalidOperationException(
                "Failed to deserialize packet: No bytes were read.")
            : packet;
    }

    /// <summary>
    /// Compresses <see cref="Data"/> using LZ4 (raw bytes, no Base64).
    /// </summary>
    /// <remarks><b>Internal infrastructure API. Do not call directly.</b></remarks>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static Binary1024 Compress(Binary1024 packet)
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
    public static Binary1024 Decompress(Binary1024 packet)
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

    /// <inheritdoc/>
    public override System.Byte[] Serialize() => LiteSerializer.Serialize(this);

    /// <inheritdoc/>
    public override void Serialize(System.Span<System.Byte> buffer) => LiteSerializer.Serialize(this, buffer);

    /// <summary>
    /// Resets this instance to its default state for pooling reuse.
    /// </summary>
    public override void ResetForPool()
    {
        this.Data = [];
        this.Flags = PacketFlags.None;
        this.Priority = PacketPriority.Normal;
        this.Transport = ProtocolType.NONE;
    }

    /// <inheritdoc/>
    public override System.String ToString() =>
        $"BINARY1024(OpCode={OpCode}, Length={Length}, Flags={Flags}, " +
        $"Priority={Priority}, Transport={Transport}, Data={Data?.Length ?? 0} bytes)";
}
