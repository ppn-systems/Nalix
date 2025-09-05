// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Attributes;
using Nalix.Common.Caching;
using Nalix.Common.Packets;
using Nalix.Common.Packets.Abstractions;
using Nalix.Common.Packets.Enums;
using Nalix.Common.Protocols;
using Nalix.Common.Serialization;
using Nalix.Common.Serialization.Attributes;
using Nalix.Shared.Injection;
using Nalix.Shared.Memory.Pooling;
using Nalix.Shared.Messaging.Binary;
using Nalix.Shared.Serialization;

namespace Nalix.Shared.Messaging.Controls;

/// <summary>
/// Represents a binary data packet used for transmitting raw bytes over the network.
/// </summary>
[PipelineManagedTransform]
[MagicNumber(FrameMagicCode.HANDSHAKE)]
[SerializePackable(SerializeLayout.Explicit)]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.Diagnostics.DebuggerDisplay("HANDSHAKE OpCode={OpCode}, Length={Length}, Flags={Flags}")]
public class Handshake : FrameBase, IPoolable, IPacketDeserializer<Handshake>
{
    /// <inheritdoc/>
    public const System.Int32 DynamicSize = 32;

    /// <summary>
    /// Gets the total length of the serialized packet in bytes, including header and content.
    /// </summary>
    [SerializeIgnore]
    public override System.UInt16 Length =>
        (System.UInt16)(PacketConstants.HeaderSize + (Data?.Length ?? 0));

    /// <summary>
    /// Gets or sets the binary content of the packet.
    /// </summary>
    [SerializeDynamicSize(DynamicSize)]
    [SerializeOrder(PacketHeaderOffset.DataRegion + 1)]
    public System.Byte[] Data { get; set; }

    /// <summary>
    /// Initializes a new <see cref="Binary128"/> with empty content.
    /// </summary>
    public Handshake()
    {
        Data = [];
        Flags = PacketFlags.None;
        Priority = PacketPriority.Normal;
        Transport = ProtocolType.NONE;
        OpCode = PacketConstants.OpCodeDefault;
        MagicNumber = (System.UInt32)FrameMagicCode.HANDSHAKE;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Handshake"/> class with the specified operation code, binary data, and transport protocol.
    /// </summary>
    /// <param name="opCode">The operation code for the handshake packet.</param>
    /// <param name="data">The binary content of the packet.</param>
    /// <param name="transport">The transport protocol to use.</param>
    public Handshake(
        System.UInt16 opCode,
        System.Byte[] data, ProtocolType transport) : base()
    {
        this.OpCode = opCode;
        this.Data = data;
        this.Transport = transport;
    }

    /// <summary>
    /// Initializes the packet with a sequence ID, binary data, and an optional transport protocol.
    /// </summary>
    /// <param name="data">Binary content of the packet.</param>
    /// <param name="transport">The transport protocol to use (default is TCP).</param>
    public void Initialize(System.Byte[] data, ProtocolType transport = ProtocolType.TCP)
    {
        this.Data = data ?? [];
        this.Transport = transport;
    }

    /// <summary>
    /// Deserializes a <see cref="Handshake"/> from the specified buffer.
    /// </summary>
    /// <param name="buffer">The source buffer.</param>
    /// <returns>A pooled <see cref="Handshake"/> instance.</returns>
    public static Handshake Deserialize(System.ReadOnlySpan<System.Byte> buffer)
    {
        Handshake packet = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                   .Get<Handshake>();

        System.Int32 bytesRead = LiteSerializer.Deserialize(buffer, ref packet);

        return bytesRead == 0
            ? throw new System.InvalidOperationException(
                "Failed to deserialize packet: No bytes were read.")
            : packet;
    }

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
        $"HANDSHAKE(OpCode={OpCode}, Length={Length}, Flags={Flags}, " +
        $"Priority={Priority}, Transport={Transport}, Data={Data?.Length ?? 0} bytes)";
}
