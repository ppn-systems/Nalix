// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Abstractions.Serialization;
using Nalix.Codec.DataFrames;
using Nalix.Environment.Time;

namespace Nalix.Codec.ProtocolFrames;

/// <summary>
/// Represents a time synchronization or ping packet used for RTT measurement and clock alignment.
/// </summary>
[Packet]
[GenerateFormatter]
[ExcludeFromCodeCoverage]
[SerializePackable(SerializeLayout.Explicit)]
[DebuggerDisplay("TIMESYNC OpCode={OpCode}, Type={Type}, Timestamp={Timestamp}, MonoTicks={MonoTicks}")]
public sealed partial class TimeSync : PacketBase<TimeSync>, IPacketTimestamped, IFixedSizeSerializable
{
    /// <summary>
    /// Gets or sets the control message type (e.g., PING, PONG, TIMESYNCREQUEST, TIMESYNCRESPONSE).
    /// </summary>
    [SerializeOrder(0)]
    public ControlType Type { get; set; }

    /// <summary>
    /// Gets or sets the timestamp associated with this packet.
    /// </summary>
    [SerializeOrder(1)]
    public long Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the monotonic timestamp (in ticks) for RTT measurement.
    /// </summary>
    [SerializeOrder(2)]
    public long MonoTicks { get; set; }

    /// <summary>
    /// Initializes a new instance of the TimeSync class with default metadata values.
    /// </summary>
    public TimeSync() => this.ResetForPool();

    /// <summary>
    /// Initializes the TimeSync packet with metadata and captures the current time.
    /// </summary>
    /// <param name="type">The control message type.</param>
    /// <param name="sequenceId">The sequence identifier (optional, default = 0).</param>
    /// <param name="flags">The packet flags (transport reliability).</param>
    public void Initialize(
        ControlType type, ushort sequenceId = 0,
        PacketFlags flags = PacketFlags.SYSTEM)
    {
        this.Type = type;
        this.Flags = flags;
        this.SequenceId = sequenceId;
        this.MonoTicks = Clock.MonoTicksNow();
        this.Timestamp = Clock.UnixMillisecondsNow();
    }

    /// <summary>
    /// Initializes the TimeSync packet with full metadata.
    /// </summary>
    /// <param name="opCode">The operation code.</param>
    /// <param name="type">The control message type.</param>
    /// <param name="sequenceId">The sequence identifier (optional, default = 0).</param>
    /// <param name="flags">The packet flags (transport reliability).</param>
    public void Initialize(
        ushort opCode, ControlType type, ushort sequenceId = 0,
        PacketFlags flags = PacketFlags.SYSTEM)
    {
        this.OpCode = opCode;
        this.Initialize(type, sequenceId, flags);
    }

    /// <inheritdoc/>
    public override void ResetForPool()
    {
        base.ResetForPool();
        this.Timestamp = 0;
        this.MonoTicks = 0;
        this.SequenceId = 0;
        this.Type = ControlType.NONE;
        this.Priority = PacketPriority.HIGH;
        this.Flags = PacketFlags.SYSTEM;
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"TimeSync(Op={this.OpCode}, Typ={this.Type}, SEQ={this.SequenceId}, Ts={this.Timestamp}, Mono={this.MonoTicks})";
}
