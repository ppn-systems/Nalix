// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Abstractions.Serialization;
using Nalix.Codec.DataFrames;

namespace Nalix.Codec.ProtocolFrames;

/// <summary>
/// Represents a binary data packet used for transmitting raw bytes over the network.
/// </summary>
[Packet]
[GenerateFormatter]
[ExcludeFromCodeCoverage]
[SerializePackable(SerializeLayout.Explicit)]
[DebuggerDisplay("CONTROL OpCode={OpCode}, Length={Length}, Flags={Flags}")]
public sealed partial class Control : PacketBase<Control>, IFixedSizeSerializable, IPacketStaticOpcode, IPacketReasoned
{
    /// <inheritdoc/>
    public static ushort StaticOpCode => (ushort)ProtocolOpCode.SYSTEM_CONTROL;

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
    /// Initializes a new instance of the Control class with default metadata values.
    /// </summary>
    public Control() => this.ResetForPool();

    /// <summary>
    /// Initializes the control packet with full metadata.
    /// </summary>
    /// <param name="type">The control message type.</param>
    /// <param name="sequenceId">The sequence identifier (optional, default = 0).</param>
    /// <param name="reasonCode">The reason code (optional, default = 0).</param>
    /// <param name="flags">The packet flags (transport reliability).</param>
    public void Initialize(
        ControlType type, ushort sequenceId = 0,
        PacketFlags flags = PacketFlags.SYSTEM,
        ProtocolReason reasonCode = ProtocolReason.NONE)
    {
        this.Type = type;
        this.Flags = flags;
        this.Reason = reasonCode;
        this.SequenceId = sequenceId;
    }



    /// <inheritdoc/>
    public override void ResetForPool()
    {
        base.ResetForPool();

        this.Reason = 0;
        this.SequenceId = 0;
        this.Type = ControlType.NONE;
        this.Priority = PacketPriority.HIGH;
        this.Flags = PacketFlags.SYSTEM;
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"Control(Op={this.OpCode}, Len={this.Length}, Flg={this.Flags}, Pri={this.Priority}, SEQ={this.SequenceId}, Rsn={this.Reason}, Typ={this.Type})";
}
