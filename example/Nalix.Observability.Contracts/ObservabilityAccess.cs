// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Abstractions.Primitives;
using Nalix.Abstractions.Security;
using Nalix.Abstractions.Serialization;
using Nalix.Codec.DataFrames;

namespace Nalix.Observability.Contracts;

[Packet]
[ExcludeFromCodeCoverage]
[GenerateFormatterAttribute]
[SerializePackable(SerializeLayout.Explicit)]
[DebuggerDisplay("OBSERVABILITY_ACCESS Stage={Stage}, Granted={AccessLevel}, Reason={Reason}")]
public sealed partial class ObservabilityAccess : PacketBase<ObservabilityAccess>, IPacketValidatable
{
    public const ushort OpCodeValue = 0x5100;

    [SerializeOrder(0)]
    public ObservabilityAccessStage Stage { get; set; }

    [SerializeOrder(1)]
    public ProtocolReason Reason { get; set; }

    [SerializeOrder(2)]
    public PermissionLevel AccessLevel { get; set; }

    [SerializeOrder(3)]
    public Bytes32 AccessKey { get; set; } = Bytes32.Zero;

    public ObservabilityAccess() => this.ResetForPool();

    public void Initialize(
        ObservabilityAccessStage stage,
        ProtocolReason reason = ProtocolReason.NONE,
        PermissionLevel AccessLevel = PermissionLevel.NONE,
        Bytes32 accessKey = default,
        PacketFlags flags = PacketFlags.SYSTEM | PacketFlags.RELIABLE)
    {
        this.OpCode = OpCodeValue;
        this.Priority = PacketPriority.URGENT;
        this.Flags = flags;
        this.Stage = stage;
        this.Reason = reason;
        this.AccessLevel = AccessLevel;
        this.AccessKey = accessKey;
    }

    public override void ResetForPool()
    {
        base.ResetForPool();
        this.OpCode = OpCodeValue;
        this.Priority = PacketPriority.URGENT;
        this.Flags = PacketFlags.SYSTEM | PacketFlags.RELIABLE;
        this.Stage = ObservabilityAccessStage.NONE;
        this.Reason = ProtocolReason.NONE;
        this.AccessLevel = PermissionLevel.NONE;
        this.AccessKey = Bytes32.Zero;
    }

    public bool Validate([NotNullWhen(false)] out string? failureReason)
    {
        bool isValid = this.Stage switch
        {
            ObservabilityAccessStage.REQUEST => !this.AccessKey.IsZero,
            ObservabilityAccessStage.RESPONSE => this.Reason != ProtocolReason.NONE || this.AccessLevel > PermissionLevel.NONE,
            ObservabilityAccessStage.NONE or _ => false
        };

        failureReason = isValid ? null : $"Invalid fields provided for observability access stage {this.Stage}.";
        return isValid;
    }
}
