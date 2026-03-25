// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Nalix.Common.Middleware;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Serialization;
using Nalix.Framework.Time;

namespace Nalix.Shared.Frames.Controls;

/// <summary>
/// Represents a binary data packet used for transmitting raw bytes over the network.
/// </summary>
[PipelineManagedTransform]
[ExcludeFromCodeCoverage]
[DebuggerDisplay("CONTROL OpCode={OpCode}, Length={Length}, Flags={Flags}")]
public sealed class Control : PacketBase<Control>, IPacketTimestamped, IPacketReasoned
{
    /// <summary>
    /// Gets or sets the reason code associated with this control packet.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.Region + 0)]
    public ProtocolReason Reason { get; set; }

    /// <summary>
    /// Gets or sets the binary content of the packet.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.Region + 1)]
    public ControlType Type { get; set; }

    /// <summary>
    /// Gets or sets the timestamp associated with this packet.s
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.Region + 2)]
    public long Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the monotonic timestamp (in ticks) for RTT measurement.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.Region + 3)]
    public long MonoTicks { get; set; }

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
        ControlType type, uint sequenceId = 0,
        ProtocolReason reasonCode = ProtocolReason.NONE, ProtocolType transport = ProtocolType.TCP)
    {
        Type = type;
        Reason = reasonCode;
        Protocol = transport;
        SequenceId = sequenceId;
        MonoTicks = Clock.MonoTicksNow();
        Timestamp = Clock.UnixMillisecondsNow();
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
        OpCode = opCode;
        Initialize(type, sequenceId, reasonCode, transport);
    }

    /// <inheritdoc/>
    public override void ResetForPool()
    {
        base.ResetForPool();
        Reason = 0;
        Timestamp = 0;
        MonoTicks = 0;
        SequenceId = 0;
        Type = ControlType.NONE;
        Priority = PacketPriority.URGENT;
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"Control(Op={OpCode}, Len={Length}, Flg={Flags}, Pri={Priority}, " +
        $"Tr={Protocol}, SEQ={SequenceId}, Rsn={Reason}, Typ={Type}, Ts={Timestamp}, Mono={MonoTicks})";
}
