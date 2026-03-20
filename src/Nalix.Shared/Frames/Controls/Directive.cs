// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Serialization;

namespace Nalix.Shared.Frames.Controls;

/// <summary>
/// A compact, generic server-to-client directive frame for common control scenarios.
/// </summary>
[SerializePackable(SerializeLayout.Explicit)]
[System.Diagnostics.DebuggerDisplay("Directive Seq={SequenceId}, Type={Type}, Reason={Reason}, Action={Action}")]
public sealed class Directive : PacketBase<Directive>, IPacketReasoned, IPacketSequenced
{
    /// <summary>
    /// DIRECTIVE type (shared ControlType).
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.DATA_REGION + 0)]
    public ControlType Type { get; set; }

    /// <summary>
    /// Reason taxonomy explaining why this directive is sent.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.DATA_REGION + 1)]
    public ProtocolReason Reason { get; set; }

    /// <summary>
    /// Suggested client action for this reason.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.DATA_REGION + 2)]
    public ProtocolAdvice Action { get; set; }

    /// <summary>
    /// Fast-path decision flags.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.DATA_REGION + 3)]
    public ControlFlags Control { get; set; }

    /// <summary>
    /// Multi-purpose argument #0.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.DATA_REGION + 4)]
    public System.UInt32 Arg0 { get; set; }

    /// <summary>
    /// Multi-purpose argument #1.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.DATA_REGION + 5)]
    public System.UInt32 Arg1 { get; set; }

    /// <summary>
    /// Multi-purpose argument #2.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.DATA_REGION + 6)]
    public System.UInt16 Arg2 { get; set; }

    /// <summary>
    /// Initialize with minimal defaults.
    /// </summary>
    public Directive()
    {
        this.Protocol = ProtocolType.TCP;
        this.Priority = PacketPriority.URGENT;
        this.OpCode = PacketConstants.OPCODE_DEFAULT;
    }

    /// <summary>
    /// Initialize all fields without allocations. Keep semantics stable across versions.
    /// </summary>
    public void Initialize(
        ControlType type, ProtocolReason reason, ProtocolAdvice action,
        System.UInt32 sequenceId, ControlFlags flags = ControlFlags.NONE,
        System.UInt32 arg0 = 0, System.UInt32 arg1 = 0, System.UInt16 arg2 = 0)
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

    /// <summary>
    /// Initialize all fields with custom opCode.
    /// </summary>
    public void Initialize(
        System.UInt16 opCode,
        ControlType type, ProtocolReason reason, ProtocolAdvice action,
        System.UInt32 sequenceId, ControlFlags flags = ControlFlags.NONE,
        System.UInt32 arg0 = 0, System.UInt32 arg1 = 0, System.UInt16 arg2 = 0)
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
    public override System.String ToString()
        => $"Directive [SequenceId={SequenceId}, Type={Type}, Reason={Reason}, Action={Action}, Control={Control}, Arg0={Arg0}, Arg1={Arg1}, Arg2={Arg2}, OpCode={OpCode}, Priority={Priority}, Protocol={Protocol}]";
}