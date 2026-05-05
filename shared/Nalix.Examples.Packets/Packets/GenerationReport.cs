// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Abstractions.Serialization;
using Nalix.Codec.DataFrames;

namespace Nalix.Examples.Packets;

public enum GenerationReportStage : byte
{
    NONE = 0x00,
    REQUEST = 0x01,
    RESPONSE = 0x02
}

public enum GenerationReportTarget : byte
{
    NONE = 0x00,
    DISPATCH = 0x01,
    TASKS = 0x02,
    BUFFERS = 0x03,
    CONNECTIONS = 0x04,
    INSTANCES = 0x05
}

[Packet]
[ExcludeFromCodeCoverage]
[SerializePackable(SerializeLayout.Explicit)]
[DebuggerDisplay("GENERATION_REPORT Stage={Stage}, Target={Target}, Reason={Reason}")]
public sealed class GenerationReport : PacketBase<GenerationReport>, IPacketValidatable
{
    public const ushort OpCodeValue = 0x5101;

    [SerializeOrder(0)]
    public GenerationReportStage Stage { get; set; }

    [SerializeOrder(1)]
    public GenerationReportTarget Target { get; set; }

    [SerializeOrder(2)]
    public ProtocolReason Reason { get; set; }

    [SerializeOrder(3)]
    [SerializeDynamicSize(4096)]
    public Dictionary<string, object>? Data { get; set; }

    public GenerationReport() => this.ResetForPool();

    public void Initialize(
        GenerationReportStage stage,
        GenerationReportTarget target,
        ProtocolReason reason = ProtocolReason.NONE,
        Dictionary<string, object>? data = null,
        PacketFlags flags = PacketFlags.SYSTEM | PacketFlags.RELIABLE)
    {
        this.OpCode = OpCodeValue;
        this.Priority = PacketPriority.HIGH;
        this.Flags = flags;
        this.Stage = stage;
        this.Target = target;
        this.Reason = reason;
        this.Data = data;
    }

    public override void ResetForPool()
    {
        base.ResetForPool();
        this.OpCode = OpCodeValue;
        this.Priority = PacketPriority.HIGH;
        this.Flags = PacketFlags.SYSTEM | PacketFlags.RELIABLE;
        this.Stage = GenerationReportStage.NONE;
        this.Target = GenerationReportTarget.NONE;
        this.Reason = ProtocolReason.NONE;
        this.Data = null;
    }

    public bool Validate([NotNullWhen(false)] out string? failureReason)
    {
        bool isValid = this.Stage switch
        {
            GenerationReportStage.REQUEST => this.Target != GenerationReportTarget.NONE,
            GenerationReportStage.RESPONSE => this.Target != GenerationReportTarget.NONE &&
                                              (this.Reason != ProtocolReason.NONE || this.Data is not null),
            GenerationReportStage.NONE or _ => false
        };

        failureReason = isValid ? null : $"Invalid fields provided for generation report stage {this.Stage}.";
        return isValid;
    }
}
