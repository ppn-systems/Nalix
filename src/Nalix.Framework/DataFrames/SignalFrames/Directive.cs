// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Serialization;

namespace Nalix.Framework.DataFrames.SignalFrames;

/// <summary>
/// Represents a directive frame used for control and server feedback.
/// </summary>
[SerializePackable(SerializeLayout.Explicit)]
[DebuggerDisplay("Directive Seq={SequenceId}, Type={Type}, Reason={Reason}, Action={Action}")]
public sealed class Directive : PacketBase<Directive>, IPacketReasoned, IFixedSizeSerializable
{
    /// <inheritdoc/>
    [SerializeIgnore]
    public static int Size => PacketConstants.HeaderSize
        + sizeof(ControlType)
        + sizeof(ProtocolReason)
        + sizeof(ProtocolAdvice)
        + sizeof(ControlFlags)
        + sizeof(uint)
        + sizeof(uint)
        + sizeof(ushort);

    /// <summary>
    /// Gets or sets the directive type.
    /// </summary>
    [SerializeOrder(0)]
    public ControlType Type { get; set; }

    /// <summary>
    /// Gets or sets the reason for the directive.
    /// </summary>
    [SerializeOrder(1)]
    public ProtocolReason Reason { get; set; }

    /// <summary>
    /// Gets or sets the suggested action for the client.
    /// </summary>
    [SerializeOrder(2)]
    public ProtocolAdvice Action { get; set; }

    /// <summary>
    /// Gets or sets directive flags.
    /// </summary>
    [SerializeOrder(3)]
    public ControlFlags Control { get; set; }

    /// <summary>Gets or sets the first directive argument.</summary>
    [SerializeOrder(4)]
    public uint Arg0 { get; set; }

    /// <summary>
    /// Gets or sets the second directive argument.
    /// </summary>
    [SerializeOrder(5)]
    public uint Arg1 { get; set; }

    /// <summary>
    /// Gets or sets the third directive argument.
    /// </summary>
    [SerializeOrder(6)]
    public ushort Arg2 { get; set; }

    /// <summary>
    /// Initializes a new instance with default values.
    /// </summary>
    public Directive()
    {
        this.Protocol = ProtocolType.TCP;
        this.Priority = PacketPriority.URGENT;
        this.OpCode = PacketConstants.OpcodeDefault;
    }

    /// <summary>Initializes the directive payload.</summary>
    /// <param name="type">The directive type.</param>
    /// <param name="reason">The reason code.</param>
    /// <param name="action">The suggested client action.</param>
    /// <param name="sequenceId">The sequence identifier.</param>
    /// <param name="flags">The directive flags.</param>
    /// <param name="arg0">The first directive argument.</param>
    /// <param name="arg1">The second directive argument.</param>
    /// <param name="arg2">The third directive argument.</param>
    public void Initialize(
        ControlType type, ProtocolReason reason, ProtocolAdvice action,
        uint sequenceId, ControlFlags flags = ControlFlags.NONE,
        uint arg0 = 0, uint arg1 = 0, ushort arg2 = 0)
    {
        this.Arg0 = arg0;
        this.Arg1 = arg1;
        this.Arg2 = arg2;
        this.Type = type;
        this.Reason = reason;
        this.Action = action;
        this.Control = flags;
        this.SequenceId = sequenceId;

        this.Protocol = ProtocolType.TCP;
        this.Priority = PacketPriority.URGENT;
    }

    /// <summary>Initializes the directive payload with a custom opcode.</summary>
    /// <param name="opCode">The opcode to assign.</param>
    /// <param name="type">The directive type.</param>
    /// <param name="reason">The reason code.</param>
    /// <param name="action">The suggested client action.</param>
    /// <param name="sequenceId">The sequence identifier.</param>
    /// <param name="flags">The directive flags.</param>
    /// <param name="arg0">The first directive argument.</param>
    /// <param name="arg1">The second directive argument.</param>
    /// <param name="arg2">The third directive argument.</param>
    public void Initialize(
        ushort opCode,
        ControlType type, ProtocolReason reason, ProtocolAdvice action,
        uint sequenceId, ControlFlags flags = ControlFlags.NONE,
        uint arg0 = 0, uint arg1 = 0, ushort arg2 = 0)
    {
        this.Arg0 = arg0;
        this.Arg1 = arg1;
        this.Arg2 = arg2;
        this.Type = type;
        this.Reason = reason;
        this.Action = action;
        this.Control = flags;
        this.OpCode = opCode;
        this.SequenceId = sequenceId;

        this.Protocol = ProtocolType.TCP;
        this.Priority = PacketPriority.URGENT;
    }

    /// <summary>
    /// Returns a string representation of the directive and all its fields.
    /// </summary>
    /// <returns>String describing the Directive packet.</returns>
    public override string ToString()
        => $"Directive [SequenceId={this.SequenceId}, Type={this.Type}, Reason={this.Reason}, Action={this.Action}, Control={this.Control}, " +
           $"Arg0={this.Arg0}, Arg1={this.Arg1}, Arg2={this.Arg2}, OpCode={this.OpCode}, Priority={this.Priority}, Protocol={this.Protocol}]";
}
