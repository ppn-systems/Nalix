// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Attributes;
using Nalix.Common.Caching;
using Nalix.Common.Packets;
using Nalix.Common.Packets.Abstractions;
using Nalix.Common.Packets.Enums;
using Nalix.Common.Protocols;
using Nalix.Common.Serialization;
using Nalix.Common.Serialization.Attributes;
using Nalix.Framework.Time;
using Nalix.Shared.Injection;
using Nalix.Shared.Memory.Pooling;
using Nalix.Shared.Messaging.Binary;
using Nalix.Shared.Serialization;

namespace Nalix.Shared.Messaging.Controls;

/// <summary>
/// Represents a binary data packet used for transmitting raw bytes over the network.
/// </summary>
[PipelineManagedTransform]
[MagicNumber(FrameMagicCode.CONTROL)]
[SerializePackable(SerializeLayout.Explicit)]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.Diagnostics.DebuggerDisplay("CONTROL OpCode={OpCode}, Length={Length}, Flags={Flags}")]
public sealed class Control : FrameBase, IPoolable, IPacketTimestamped, IPacketReasoned, IPacketSequenced, IPacketDeserializer<Control>
{
    /// <summary>
    /// Gets the total length of the serialized packet in bytes, including header and content.
    /// </summary>
    [SerializeIgnore]
    public override System.UInt16 Length =>
        PacketConstants.HeaderSize
        + sizeof(ControlType)    // ControlType
        + sizeof(System.Int64)   // Timestamp
        + sizeof(System.Int64)   // MonoTicks
        + sizeof(System.UInt32)  // SequenceId
        + sizeof(ProtocolCode); // Reason

    /// <summary>
    /// Gets or sets the sequence identifier for this packet.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.DataRegion + 0)]
    public System.UInt32 SequenceId { get; set; }

    /// <summary>
    /// Gets or sets the reason code associated with this control packet.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.DataRegion + 1)]
    public ProtocolCode Reason { get; set; }

    /// <summary>
    /// Gets or sets the binary content of the packet.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.DataRegion + 2)]
    public ControlType Type { get; set; }

    /// <summary>
    /// Gets or sets the timestamp associated with this packet.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.DataRegion + 3)]
    public System.Int64 Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the monotonic timestamp (in ticks) for RTT measurement.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.DataRegion + 4)]
    public System.Int64 MonoTicks { get; set; }

    /// <summary>
    /// Initializes a new <see cref="Binary128"/> with empty content.
    /// </summary>
    public Control()
    {
        this.Timestamp = 0;
        this.MonoTicks = 0;
        this.SequenceId = 0;
        this.Reason = 0;
        this.Type = ControlType.NONE; // Default type, can be changed later
        this.Flags = PacketFlags.None;
        this.Priority = PacketPriority.Urgent;
        this.Transport = ProtocolType.NONE;
        this.OpCode = PacketConstants.OpCodeDefault;
        this.MagicNumber = (System.UInt32)FrameMagicCode.CONTROL;
    }

    /// <summary>
    /// Initializes the control packet with full metadata.
    /// </summary>
    /// <param name="type">The control message type.</param>
    /// <param name="sequenceId">The sequence identifier (optional, default = 0).</param>
    /// <param name="reasonCode">The reason code (optional, default = 0).</param>
    /// <param name="transport">The transport protocol (default = TCP).</param>
    public void Initialize(
        ControlType type,
        System.UInt32 sequenceId = 0,
        ProtocolCode reasonCode = ProtocolCode.NONE,
        ProtocolType transport = ProtocolType.TCP)
    {
        this.Type = type;
        this.Transport = transport;
        this.SequenceId = sequenceId;
        this.Reason = reasonCode;
        this.MonoTicks = Clock.MonoTicksNow();
        this.Timestamp = Clock.UnixMillisecondsNow();
    }

    /// <summary>
    /// Initializes the packet with binary data and a transport protocol.
    /// </summary>
    /// <param name="type">Binary content of the packet.</param>
    /// <param name="transport">The target transport protocol.</param>
    public void Initialize(ControlType type, ProtocolType transport = ProtocolType.TCP) => Initialize(type, 0, 0, transport);

    /// <summary>
    /// Deserializes a <see cref="Binary128"/> from the specified buffer.
    /// </summary>
    /// <param name="buffer">The source buffer.</param>
    /// <returns>A pooled <see cref="Binary128"/> instance.</returns>
    public static Control Deserialize(System.ReadOnlySpan<System.Byte> buffer)
    {
        Control packet = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                 .Get<Control>();

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
        this.Timestamp = 0;
        this.MonoTicks = 0;
        this.SequenceId = 0;
        this.Reason = 0;
        this.Type = ControlType.NONE;
        this.Flags = PacketFlags.None;
        this.Priority = PacketPriority.Urgent;
        this.Transport = ProtocolType.NONE;
    }

    /// <inheritdoc/>
    public override System.String ToString() =>
        $"CONTROL(Op={OpCode}, Len={Length}, Flg={Flags}, Pri={Priority}, " +
        $"Tr={Transport}, Seq={SequenceId}, Rsn={Reason}, Typ={Type}, Ts={Timestamp}, Mono={MonoTicks})";
}
