// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Abstractions.Serialization;
using Nalix.Codec.DataFrames;
using Nalix.Environment.Memory;

namespace Nalix.Observability.Contracts;

/// <summary>
/// Represents a packet used to request and return runtime observation data.
/// </summary>
/// <remarks>
/// A request identifies a <see cref="RuntimeObservationTarget"/>. A response carries serialized
/// observation data for that target, or a <see cref="ProtocolReason"/> when the request cannot be served.
/// </remarks>
[Packet]
[ExcludeFromCodeCoverage]
[GenerateFormatterAttribute]
[SerializePackable(SerializeLayout.Explicit)]
[DebuggerDisplay("RUNTIME_OBSERVATION Stage={Stage}, Target={Target}, Reason={Reason}")]
public sealed partial class RuntimeObservation : PacketBase<RuntimeObservation>, IPacketValidatable
{
    /// <summary>
    /// Gets the protocol opcode assigned to runtime observation packets.
    /// </summary>
    public const ushort OpCodeValue = 0x0C;

    /// <summary>
    /// Gets or sets the observation packet lifecycle stage.
    /// </summary>
    [SerializeOrder(0)]
    public RuntimeObservationStage Stage { get; set; }

    /// <summary>
    /// Gets or sets the runtime subsystem targeted by the observation request.
    /// </summary>
    [SerializeOrder(1)]
    public RuntimeObservationTarget Target { get; set; }

    /// <summary>
    /// Gets or sets the protocol reason returned by the responder.
    /// </summary>
    [SerializeOrder(2)]
    public ProtocolReason Reason { get; set; }

    /// <summary>
    /// Gets or sets the serialized observation payload returned by a response.
    /// </summary>
    [SerializeOrder(3)]
    public ReadOnlyMemory<byte> ObservationData { get; set; } = ReadOnlyMemory<byte>.Empty;

    [SerializeIgnore]
    private BufferLease? _lease;

    /// <summary>
    /// Associates a rented buffer lease with the packet and transfers ownership to the packet.
    /// </summary>
    /// <remarks>
    /// The lease is disposed when the packet is reset, which returns the underlying buffer to the pool.
    /// </remarks>
    /// <param name="lease">The rented buffer lease to associate.</param>
    public void AssociateLease(BufferLease lease)
    {
        ArgumentNullException.ThrowIfNull(lease);
        _lease?.Dispose();
        _lease = lease;
        this.ObservationData = lease.Memory;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeObservation"/> class.
    /// </summary>
    public RuntimeObservation() => this.ResetForPool();

    /// <summary>
    /// Initializes the packet fields for a runtime observation exchange.
    /// </summary>
    /// <param name="stage">The observation lifecycle stage represented by the packet.</param>
    /// <param name="target">The runtime subsystem targeted by the packet.</param>
    /// <param name="reason">The protocol reason for a response, or <see cref="ProtocolReason.NONE"/> for a request.</param>
    /// <param name="ObservationData">The serialized observation data supplied by a response.</param>
    /// <param name="flags">The packet flags to apply.</param>
    public void Initialize(
        RuntimeObservationStage stage,
        RuntimeObservationTarget target,
        ProtocolReason reason = ProtocolReason.NONE,
        ReadOnlyMemory<byte> ObservationData = default,
        PacketFlags flags = PacketFlags.SYSTEM | PacketFlags.RELIABLE)
    {
        this.OpCode = OpCodeValue;
        this.Priority = PacketPriority.HIGH;
        this.Flags = flags;
        this.Stage = stage;
        this.Target = target;
        this.Reason = reason;
        this.ObservationData = ObservationData;
    }

    /// <summary>
    /// Resets the packet to its pooled default state and releases any associated buffer lease.
    /// </summary>
    public override void ResetForPool()
    {
        base.ResetForPool();
        this.OpCode = OpCodeValue;
        this.Priority = PacketPriority.HIGH;
        this.Flags = PacketFlags.SYSTEM | PacketFlags.RELIABLE;
        this.Stage = RuntimeObservationStage.NONE;
        this.Target = RuntimeObservationTarget.NONE;
        this.Reason = ProtocolReason.NONE;
        this.ObservationData = ReadOnlyMemory<byte>.Empty;
        _lease?.Dispose();
        _lease = null;
    }

    /// <summary>
    /// Validates whether the packet contains the required fields for its current <see cref="Stage"/>.
    /// </summary>
    /// <param name="failureReason">When validation fails, receives a message describing the invalid state.</param>
    /// <returns><see langword="true"/> when the packet is valid; otherwise, <see langword="false"/>.</returns>
    public bool Validate([NotNullWhen(false)] out string? failureReason)
    {
        bool isValid = this.Stage switch
        {
            RuntimeObservationStage.REQUEST => this.Target != RuntimeObservationTarget.NONE,
            RuntimeObservationStage.RESPONSE => this.Target != RuntimeObservationTarget.NONE &&
                                               (this.Reason != ProtocolReason.NONE || !this.ObservationData.IsEmpty),
            RuntimeObservationStage.NONE or _ => false
        };

        failureReason = isValid ? null : $"Invalid fields provided for runtime observation stage {this.Stage}.";
        return isValid;
    }
}
