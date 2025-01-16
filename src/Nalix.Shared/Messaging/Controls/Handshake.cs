// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Attributes;
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
[MagicNumber(FrameMagic.Handshake)]
[SerializePackable(SerializeLayout.Explicit)]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.Diagnostics.DebuggerDisplay("Handshake OpCode={OpCode}, Length={Length}, Flags={Flags}")]
public class Handshake : FrameBase, IPacketDeserializer<Handshake>
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
    [SerializeOrder(PacketHeaderOffset.DataRegion)]
    public System.Byte[] Data { get; set; }

    /// <summary>
    /// Initializes a new <see cref="Binary128"/> with empty content.
    /// </summary>
    public Handshake()
    {
        Data = [];
        Flags = PacketFlags.None;
        Priority = PacketPriority.Normal;
        Transport = TransportProtocol.NONE;
        OpCode = PacketConstants.OpCodeDefault;
        MagicNumber = (System.UInt32)FrameMagic.Handshake;
    }

    /// <summary>
    /// Initializes the packet with binary data.
    /// </summary>
    /// <param name="data">Binary content of the packet.</param>
    public void Initialize(System.Byte[] data) => Initialize(data, TransportProtocol.NONE);

    /// <summary>
    /// Initializes the packet with binary data and a transport protocol.
    /// </summary>
    /// <param name="data">Binary content of the packet.</param>
    /// <param name="transport">The target transport protocol.</param>
    public void Initialize(System.Byte[] data, TransportProtocol transport = TransportProtocol.TCP)
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
        this.Transport = TransportProtocol.NONE;
    }

    /// <inheritdoc/>
    public override System.String ToString() =>
        $"HANDSHAKE(OpCode={OpCode}, Length={Length}, Flags={Flags}, " +
        $"Priority={Priority}, Transport={Transport}, Data={Data?.Length ?? 0} bytes)";
}
