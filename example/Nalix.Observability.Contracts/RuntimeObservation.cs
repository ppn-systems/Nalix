// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Abstractions.Serialization;
using Nalix.Codec.DataFrames;

namespace Nalix.Observability.Contracts;

[Packet]
[ExcludeFromCodeCoverage]
[GenerateFormatterAttribute]
[SerializePackable(SerializeLayout.Explicit)]
[DebuggerDisplay("RUNTIME_OBSERVATION Stage={Stage}, Target={Target}, Reason={Reason}")]
public sealed partial class RuntimeObservation : PacketBase<RuntimeObservation>, IPacketValidatable
{
    public const ushort OpCodeValue = 0x5101;

    [SerializeOrder(0)]
    public RuntimeObservationStage Stage { get; set; }

    [SerializeOrder(1)]
    public RuntimeObservationTarget Target { get; set; }

    [SerializeOrder(2)]
    public ProtocolReason Reason { get; set; }

    [SerializeOrder(3)]
    [SerializeDynamicSize(64 * 1024)]
    public string ObservationData { get; set; } = string.Empty;

    public RuntimeObservation() => this.ResetForPool();

    public void Initialize(
        RuntimeObservationStage stage,
        RuntimeObservationTarget target,
        ProtocolReason reason = ProtocolReason.NONE,
        string? ObservationData = null,
        PacketFlags flags = PacketFlags.SYSTEM | PacketFlags.RELIABLE)
    {
        this.OpCode = OpCodeValue;
        this.Priority = PacketPriority.HIGH;
        this.Flags = flags;
        this.Stage = stage;
        this.Target = target;
        this.Reason = reason;
        this.ObservationData = ObservationData ?? "{}";
    }

    public override void ResetForPool()
    {
        base.ResetForPool();
        this.OpCode = OpCodeValue;
        this.Priority = PacketPriority.HIGH;
        this.Flags = PacketFlags.SYSTEM | PacketFlags.RELIABLE;
        this.Stage = RuntimeObservationStage.NONE;
        this.Target = RuntimeObservationTarget.NONE;
        this.Reason = ProtocolReason.NONE;
        this.ObservationData = string.Empty;
    }

    public bool Validate([NotNullWhen(false)] out string? failureReason)
    {
        bool isValid = this.Stage switch
        {
            RuntimeObservationStage.REQUEST => this.Target != RuntimeObservationTarget.NONE,
            RuntimeObservationStage.RESPONSE => this.Target != RuntimeObservationTarget.NONE &&
                                              (this.Reason != ProtocolReason.NONE || !string.IsNullOrWhiteSpace(this.ObservationData)),
            RuntimeObservationStage.NONE or _ => false
        };

        failureReason = isValid ? null : $"Invalid fields provided for runtime observation stage {this.Stage}.";
        return isValid;
    }
}
