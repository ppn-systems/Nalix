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
using Nalix.Shared.Serialization;

namespace Nalix.Shared.Messaging.Controls;

/// <summary>
/// A compact, generic server-to-client directive frame for common control scenarios.
/// </summary>
[SerializePackable(SerializeLayout.Explicit)]
[MagicNumber(FrameMagic.DIRECTIVE)]
[System.Diagnostics.DebuggerDisplay("DIRECTIVE Seq={SequenceId}, Type={Type}, Reason={Reason}, Action={Action}")]
public sealed class Directive : FrameBase, IPacketReasoned, IPacketSequenced, IPacketDeserializer<Directive>
{
    /// <inheritdoc/>
    [SerializeIgnore]
    public override System.UInt16 Length =>
        PacketConstants.HeaderSize
        + sizeof(System.UInt32)     // SequenceId
        + sizeof(ControlType)       // Type (ControlType)
        + sizeof(ReasonCode)        // Reason (Reason)
        + sizeof(SuggestedAction)   // Action (SuggestedAction)
        + sizeof(ControlFlags)      // Flags (CONTROL)
        + sizeof(System.UInt32)     // Arg0
        + sizeof(System.UInt32)     // Arg1
        + sizeof(System.UInt16);    // Arg2

    /// <summary>
    /// Round-trip correlation to the triggering request.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.DataRegion + 0)]
    public System.UInt32 SequenceId { get; set; }

    /// <summary>
    /// DIRECTIVE type (shared ControlType). Example: NACK, THROTTLE, REDIRECT, NOTICE.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.DataRegion + 1)]
    public ControlType Type { get; set; }

    /// <summary>
    /// Reason taxonomy explaining why this directive is sent.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.DataRegion + 2)]
    public ReasonCode Reason { get; set; }

    /// <summary>
    /// Suggested client action for this reason.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.DataRegion + 3)]
    public SuggestedAction Action { get; set; }

    /// <summary>
    /// Fast-path decision flags (see <see cref="ControlFlags"/>).
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.DataRegion + 4)]
    public ControlFlags Control { get; set; }

    /// <summary>
    /// Multi-purpose argument #0. Ex: RetryAfterSteps (100ms units) or RedirectHostHash.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.DataRegion + 5)]
    public System.UInt32 Arg0 { get; set; }

    /// <summary>
    /// Multi-purpose argument #1. Ex: DetailId (client i18n) or ResourceIdHash.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.DataRegion + 6)]
    public System.UInt32 Arg1 { get; set; }

    /// <summary>
    /// Multi-purpose argument #2. Ex: RedirectPort or credit/window size.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.DataRegion + 7)]
    public System.UInt16 Arg2 { get; set; }

    /// <summary>Initialize with minimal defaults.</summary>
    public Directive()
    {
        Priority = PacketPriority.Urgent;
        Transport = TransportProtocol.TCP;
        OpCode = PacketConstants.OpCodeDefault;
        MagicNumber = (System.UInt32)FrameMagic.DIRECTIVE;
    }

    /// <summary>
    /// Initialize all fields without allocations. Keep semantics stable across versions.
    /// </summary>
    public void Initialize(
        ControlType type,
        ReasonCode reason,
        SuggestedAction action,
        System.UInt32 sequenceId,
        ControlFlags flags = ControlFlags.NONE,
        System.UInt32 arg0 = 0,
        System.UInt32 arg1 = 0,
        System.UInt16 arg2 = 0)
    {
        Arg0 = arg0;
        Arg1 = arg1;
        Arg2 = arg2;
        Type = type;
        Reason = reason;
        Action = action;
        Control = flags;
        SequenceId = sequenceId;

        Priority = PacketPriority.Urgent;
        Transport = TransportProtocol.TCP;
    }

    /// <summary>
    /// Deserialize from the given buffer using the common serializer.
    /// </summary>
    public static Directive Deserialize(System.ReadOnlySpan<System.Byte> buffer)
    {
        var pkt = InstanceManager.Instance
                     .GetOrCreateInstance<ObjectPoolManager>()
                     .Get<Directive>();

        System.Int32 read = LiteSerializer.Deserialize(buffer, ref pkt);
        return read == 0
            ? throw new System.InvalidOperationException("Failed to deserialize DIRECTIVE: empty read.") : pkt;
    }

    /// <inheritdoc/>
    public override void ResetForPool()
    {
        Arg0 = 0;
        Arg1 = 0;
        Arg2 = 0;
        SequenceId = 0;
        Type = ControlType.NONE;
        Reason = ReasonCode.NONE;
        Control = ControlFlags.NONE;
        Action = SuggestedAction.NONE;
        Priority = PacketPriority.Urgent;
        Transport = TransportProtocol.NONE;
    }
}
