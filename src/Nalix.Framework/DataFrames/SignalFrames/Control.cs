// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Serialization;
using Nalix.Framework.Time;

namespace Nalix.Framework.DataFrames.SignalFrames;

/// <summary>
/// Represents a binary data packet used for transmitting raw bytes over the network.
/// </summary>
[Packet]
[ExcludeFromCodeCoverage]
[DebuggerDisplay("CONTROL OpCode={OpCode}, Length={Length}, Flags={Flags}")]
public sealed class Control : PacketBase<Control>, IPacketTimestamped, IPacketReasoned, IFixedSizeSerializable
{
    /// <inheritdoc/>
    [SerializeIgnore]
    public static int Size => PacketConstants.HeaderSize
        + sizeof(ProtocolReason)
        + sizeof(ControlType)
        + sizeof(long)
        + sizeof(long);

    /// <summary>
    /// Gets or sets the reason code associated with this control packet.
    /// </summary>
    [SerializeOrder(0)]
    public ProtocolReason Reason { get; set; }

    /// <summary>
    /// Gets or sets the binary content of the packet.
    /// </summary>
    [SerializeOrder(1)]
    public ControlType Type { get; set; }

    /// <summary>
    /// Gets or sets the timestamp associated with this packet.s
    /// </summary>
    [SerializeOrder(2)]
    public long Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the monotonic timestamp (in ticks) for RTT measurement.
    /// </summary>
    [SerializeOrder(3)]
    public long MonoTicks { get; set; }

    /// <summary>
    /// Initializes a new instance of the Control class with default metadata values.
    /// </summary>
    public Control() => this.ResetForPool();

    /// <summary>
    /// Initializes the control packet with full metadata.
    /// </summary>
    /// <param name="type">The control message type.</param>
    /// <param name="sequenceId">The sequence identifier (optional, default = 0).</param>
    /// <param name="reasonCode">The reason code (optional, default = 0).</param>
    /// <param name="transport">The transport protocol (default = TCP).</param>
    public void Initialize(
        ControlType type, uint sequenceId = 0,
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
        ushort opCode, ControlType type, uint sequenceId = 0,
        ProtocolReason reasonCode = ProtocolReason.NONE, ProtocolType transport = ProtocolType.TCP)
    {
        this.OpCode = opCode;
        this.Initialize(type, sequenceId, reasonCode, transport);
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
        this.Priority = PacketPriority.HIGH;
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"Control(Op={this.OpCode}, Len={this.Length}, Flg={this.Flags}, Pri={this.Priority}, " +
        $"Tr={this.Protocol}, SEQ={this.SequenceId}, Rsn={this.Reason}, Typ={this.Type}, Ts={this.Timestamp}, Mono={this.MonoTicks})";
}
