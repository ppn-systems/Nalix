// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Serialization;

namespace Nalix.Shared.Frames.Controls;

/// <summary>
/// A compact, generic server-to-client directive frame for common control scenarios.
/// </summary>
[SerializePackable(SerializeLayout.Explicit)]
[DebuggerDisplay("Directive Seq={SequenceId}, Type={Type}, Reason={Reason}, Action={Action}")]
public sealed class Directive : PacketBase<Directive>, IPacketReasoned, IPacketSequenced
{
    /// <summary>
    /// DIRECTIVE type (shared ControlType).
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.Region + 0)]
    public ControlType Type { get; set; }

    /// <summary>
    /// Reason taxonomy explaining why this directive is sent.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.Region + 1)]
    public ProtocolReason Reason { get; set; }

    /// <summary>
    /// Suggested client action for this reason.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.Region + 2)]
    public ProtocolAdvice Action { get; set; }

    /// <summary>
    /// Fast-path decision flags.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.Region + 3)]
    public ControlFlags Control { get; set; }

    /// <summary>
    /// Multi-purpose argument #0.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.Region + 4)]
    public uint Arg0 { get; set; }

    /// <summary>
    /// Multi-purpose argument #1.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.Region + 5)]
    public uint Arg1 { get; set; }

    /// <summary>
    /// Multi-purpose argument #2.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.Region + 6)]
    public ushort Arg2 { get; set; }

    /// <summary>
    /// Initialize with minimal defaults.
    /// </summary>
    public Directive()
    {
        Protocol = ProtocolType.TCP;
        Priority = PacketPriority.URGENT;
        OpCode = PacketConstants.OpcodeDefault;
    }

    /// <summary>
    /// Initialize all fields without allocations. Keep semantics stable across versions.
    /// </summary>
    /// <param name="type"></param>
    /// <param name="reason"></param>
    /// <param name="action"></param>
    /// <param name="sequenceId"></param>
    /// <param name="flags"></param>
    /// <param name="arg0"></param>
    /// <param name="arg1"></param>
    /// <param name="arg2"></param>
    public void Initialize(
        ControlType type, ProtocolReason reason, ProtocolAdvice action,
        uint sequenceId, ControlFlags flags = ControlFlags.NONE,
        uint arg0 = 0, uint arg1 = 0, ushort arg2 = 0)
    {
        Arg0 = arg0;
        Arg1 = arg1;
        Arg2 = arg2;
        Type = type;
        Reason = reason;
        Action = action;
        Control = flags;
        SequenceId = sequenceId;

        Protocol = ProtocolType.TCP;
        Priority = PacketPriority.URGENT;
    }

    /// <summary>
    /// Initialize all fields with custom opCode.
    /// </summary>
    /// <param name="opCode"></param>
    /// <param name="type"></param>
    /// <param name="reason"></param>
    /// <param name="action"></param>
    /// <param name="sequenceId"></param>
    /// <param name="flags"></param>
    /// <param name="arg0"></param>
    /// <param name="arg1"></param>
    /// <param name="arg2"></param>
    public void Initialize(
        ushort opCode,
        ControlType type, ProtocolReason reason, ProtocolAdvice action,
        uint sequenceId, ControlFlags flags = ControlFlags.NONE,
        uint arg0 = 0, uint arg1 = 0, ushort arg2 = 0)
    {
        Arg0 = arg0;
        Arg1 = arg1;
        Arg2 = arg2;
        Type = type;
        Reason = reason;
        Action = action;
        Control = flags;
        OpCode = opCode;
        SequenceId = sequenceId;

        Protocol = ProtocolType.TCP;
        Priority = PacketPriority.URGENT;
    }

    /// <summary>
    /// Returns a string representation of the directive and all its fields.
    /// </summary>
    /// <returns>String describing the Directive packet.</returns>
    public override string ToString()
        => $"Directive [SequenceId={SequenceId}, Type={Type}, Reason={Reason}, Action={Action}, Control={Control}, Arg0={Arg0}, Arg1={Arg1}, Arg2={Arg2}, OpCode={OpCode}, Priority={Priority}, Protocol={Protocol}]";
}
