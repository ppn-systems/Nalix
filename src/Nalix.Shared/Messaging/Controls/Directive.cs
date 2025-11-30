// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Attributes;
using Nalix.Common.Caching;
using Nalix.Common.Packets;
using Nalix.Common.Packets.Abstractions;
using Nalix.Common.Protocols;
using Nalix.Common.Serialization;
using Nalix.Framework.Injection;
using Nalix.Shared.Memory.Pooling;
using Nalix.Shared.Serialization;

namespace Nalix.Shared.Messaging.Controls;

/// <summary>
/// A compact, generic server-to-client directive frame for common control scenarios.
/// </summary>
[SerializePackable(SerializeLayout.Explicit)]
[MagicNumber(ProtocolMagic.DIRECTIVE)]
[System.Diagnostics.DebuggerDisplay("DIRECTIVE SEQ={SequenceId}, TYPE={TYPE}, Reason={Reason}, Action={Action}")]
public sealed class Directive : FrameBase, IPoolable, IPacketReasoned, IPacketSequenced, IPacketDeserializer<Directive>
{
    /// <inheritdoc/>
    [SerializeIgnore]
    public override System.UInt16 Length =>
        PacketConstants.HeaderSize
        + sizeof(System.UInt32)     // SequenceId
        + sizeof(ControlType)       // Type (ControlType)
        + sizeof(ProtocolReason)      // Reason (Reason)
        + sizeof(ProtocolAdvice)    // Action (ProtocolAction)
        + sizeof(ControlFlags)      // Flags (CONTROL)
        + sizeof(System.UInt32)     // Arg0
        + sizeof(System.UInt32)     // Arg1
        + sizeof(System.UInt16);    // Arg2

    /// <summary>
    /// Round-trip correlation to the triggering request.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.DATA_REGION + 0)]
    public System.UInt32 SequenceId { get; set; }

    /// <summary>
    /// DIRECTIVE type (shared ControlType). Example: NACK, THROTTLE, REDIRECT, NOTICE.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.DATA_REGION + 1)]
    public ControlType Type { get; set; }

    /// <summary>
    /// Reason taxonomy explaining why this directive is sent.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.DATA_REGION + 2)]
    public ProtocolReason Reason { get; set; }

    /// <summary>
    /// Suggested client action for this reason.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.DATA_REGION + 3)]
    public ProtocolAdvice Action { get; set; }

    /// <summary>
    /// Fast-path decision flags (see <see cref="ControlFlags"/>).
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.DATA_REGION + 4)]
    public ControlFlags Control { get; set; }

    /// <summary>
    /// Multi-purpose argument #0. Ex: RetryAfterSteps (100ms units) or RedirectHostHash.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.DATA_REGION + 5)]
    public System.UInt32 Arg0 { get; set; }

    /// <summary>
    /// Multi-purpose argument #1. Ex: DetailId (client i18n) or ResourceIdHash.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.DATA_REGION + 6)]
    public System.UInt32 Arg1 { get; set; }

    /// <summary>
    /// Multi-purpose argument #2. Ex: RedirectPort or credit/window size.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.DATA_REGION + 7)]
    public System.UInt16 Arg2 { get; set; }

    /// <summary>Initialize with minimal defaults.</summary>
    public Directive()
    {
        this.Protocol = ProtocolType.TCP;
        this.Priority = PacketPriority.URGENT;
        this.OpCode = PacketConstants.OPCODE_DEFAULT;
        this.MagicNumber = (System.UInt32)ProtocolMagic.DIRECTIVE;
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
    /// Initialize all fields without allocations. Keep semantics stable across versions.
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
    /// FromBytes from the given buffer using the common serializer.
    /// </summary>
    public static Directive Deserialize(System.ReadOnlySpan<System.Byte> buffer)
    {
        Directive packet = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                   .Get<Directive>();

        System.Int32 bytesRead = LiteSerializer.Deserialize(buffer, ref packet);
        if (bytesRead == 0)
        {
            InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .Return(packet);
            throw new System.InvalidOperationException("Failed to deserialize packet: No bytes were read.");
        }

        return packet;
    }

    /// <inheritdoc/>
    public override System.Byte[] Serialize() => LiteSerializer.Serialize(this);

    /// <inheritdoc/>
    public override System.Int32 Serialize(System.Span<System.Byte> buffer) => LiteSerializer.Serialize(this, buffer);

    /// <inheritdoc/>
    public override void ResetForPool()
    {
        this.Arg0 = 0;
        this.Arg1 = 0;
        this.Arg2 = 0;
        this.SequenceId = 0;
        this.Type = ControlType.NONE;
        this.Reason = ProtocolReason.NONE;
        this.Control = ControlFlags.NONE;
        this.Action = ProtocolAdvice.NONE;
        this.Protocol = ProtocolType.NONE;
        this.Priority = PacketPriority.URGENT;
    }
}
