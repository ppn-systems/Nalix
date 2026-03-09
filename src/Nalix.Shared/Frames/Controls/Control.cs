// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Middleware.Attributes;
using Nalix.Common.Networking.Packets.Abstractions;
using Nalix.Common.Networking.Packets.Enums;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Serialization.Attributes;
using Nalix.Framework.Time;

namespace Nalix.Shared.Frames.Controls;

/// <summary>
/// Represents a binary data packet used for transmitting raw bytes over the network.
/// </summary>
[PipelineManagedTransform]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.Diagnostics.DebuggerDisplay("CONTROL OP_CODE={OpCode}, Length={Length}, FLAGS={Flags}")]
public sealed class Control : PacketBase<Control>, IPacketTimestamped, IPacketReasoned, IPacketSequenced
{
    /// <summary>
    /// Gets or sets the sequence identifier for this packet.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.DATA_REGION + 0)]
    public System.UInt32 SequenceId { get; set; }

    /// <summary>
    /// Gets or sets the reason code associated with this control packet.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.DATA_REGION + 1)]
    public ProtocolReason Reason { get; set; }

    /// <summary>
    /// Gets or sets the binary content of the packet.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.DATA_REGION + 2)]
    public ControlType Type { get; set; }

    /// <summary>
    /// Gets or sets the timestamp associated with this packet.s
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.DATA_REGION + 3)]
    public System.Int64 Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the monotonic timestamp (in ticks) for RTT measurement.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.DATA_REGION + 4)]
    public System.Int64 MonoTicks { get; set; }

    /// <summary>
    /// Initializes a new instance of the Control class with default metadata values.
    /// </summary>
    public Control() => ResetForPool();

    /// <summary>
    /// Initializes the control packet with full metadata.
    /// </summary>
    /// <param name="type">The control message type.</param>
    /// <param name="sequenceId">The sequence identifier (optional, default = 0).</param>
    /// <param name="reasonCode">The reason code (optional, default = 0).</param>
    /// <param name="transport">The transport protocol (default = TCP).</param>
    public void Initialize(
        ControlType type, System.UInt32 sequenceId = 0,
        ProtocolReason reasonCode = ProtocolReason.NONE, ProtocolType transport = ProtocolType.TCP)
    {
        this.Type = type;
        this.Reason = reasonCode;
        this.Protocol = transport;
        this.SequenceId = sequenceId;
        this.MonoTicks = Clock.MonoTicksNow();
        this.Timestamp = Clock.UnixMillisecondsNow();
    }

    /// <summary>
    /// Initializes the control packet with full metadata.
    /// </summary>
    /// <param name="opCode">The operation code.</param>
    /// <param name="type">The control message type.</param>
    /// <param name="sequenceId">The sequence identifier (optional, default = 0).</param>
    /// <param name="reasonCode">The reason code (optional, default = 0).</param>
    /// <param name="transport">The transport protocol (default = TCP).</param>
    public void Initialize(
        System.UInt16 opCode, ControlType type, System.UInt32 sequenceId = 0,
        ProtocolReason reasonCode = ProtocolReason.NONE, ProtocolType transport = ProtocolType.TCP)
    {
        this.OpCode = opCode;
        Initialize(type, sequenceId, reasonCode, transport);
    }

    /// <inheritdoc/>
    public override void ResetForPool()
    {
        base.ResetForPool();
        this.Reason = 0;
        this.Timestamp = 0;
        this.MonoTicks = 0;
        this.SequenceId = 0;
        this.Type = ControlType.NONE;
        this.Priority = PacketPriority.URGENT;
    }

    /// <inheritdoc/>
    public override System.String ToString() =>
        $"Control(Op={OpCode}, Len={Length}, Flg={Flags}, Pri={Priority}, " +
        $"Tr={Protocol}, SEQ={SequenceId}, Rsn={Reason}, Typ={Type}, Ts={Timestamp}, Mono={MonoTicks})";
}
