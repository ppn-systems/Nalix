// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Abstractions.Security;
using Nalix.Abstractions.Serialization;
using Nalix.Codec.DataFrames;

namespace Nalix.Observability.Contracts;

public enum ObservabilityAccessStage : byte
{
    NONE = 0x00,
    REQUEST = 0x01,
    RESPONSE = 0x02
}

[Packet]
[ExcludeFromCodeCoverage]
[GenerateFormatterAttribute]
[SerializePackable(SerializeLayout.Explicit)]
[DebuggerDisplay("OBSERVABILITY_ACCESS Stage={Stage}, Granted={GrantedLevel}, Reason={Reason}")]
public sealed partial class ObservabilityAccess : PacketBase<ObservabilityAccess>, IPacketValidatable
{
    public const ushort OpCodeValue = 0x5100;

    [SerializeOrder(0)]
    public ObservabilityAccessStage Stage { get; set; }

    [SerializeOrder(1)]
    public ProtocolReason Reason { get; set; }

    [SerializeOrder(2)]
    public PermissionLevel GrantedLevel { get; set; }

    [SerializeOrder(3)]
    [SerializeDynamicSize(256)]
    public string Key { get; set; } = string.Empty;

    public ObservabilityAccess() => this.ResetForPool();

    public void Initialize(
        ObservabilityAccessStage stage,
        ProtocolReason reason = ProtocolReason.NONE,
        PermissionLevel grantedLevel = PermissionLevel.NONE,
        string key = "",
        PacketFlags flags = PacketFlags.SYSTEM | PacketFlags.RELIABLE)
    {
        this.OpCode = OpCodeValue;
        this.Priority = PacketPriority.URGENT;
        this.Flags = flags;
        this.Stage = stage;
        this.Reason = reason;
        this.GrantedLevel = grantedLevel;
        this.Key = key;
    }

    public override void ResetForPool()
    {
        base.ResetForPool();
        this.OpCode = OpCodeValue;
        this.Priority = PacketPriority.URGENT;
        this.Flags = PacketFlags.SYSTEM | PacketFlags.RELIABLE;
        this.Stage = ObservabilityAccessStage.NONE;
        this.Reason = ProtocolReason.NONE;
        this.GrantedLevel = PermissionLevel.NONE;
        this.Key = string.Empty;
    }

    public bool Validate([NotNullWhen(false)] out string? failureReason)
    {
        bool isValid = this.Stage switch
        {
            ObservabilityAccessStage.REQUEST => !string.IsNullOrWhiteSpace(this.Key),
            ObservabilityAccessStage.RESPONSE => this.Reason != ProtocolReason.NONE || this.GrantedLevel > PermissionLevel.NONE,
            ObservabilityAccessStage.NONE or _ => false
        };

        failureReason = isValid ? null : $"Invalid fields provided for observability access stage {this.Stage}.";
        return isValid;
    }
}
