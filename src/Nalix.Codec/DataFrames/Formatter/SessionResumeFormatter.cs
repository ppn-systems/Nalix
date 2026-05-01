// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Abstractions.Primitives;
using Nalix.Codec.DataFrames.SignalFrames;
using Nalix.Codec.Extensions;
using Nalix.Codec.Memory;
using Nalix.Codec.Serialization;

#pragma warning disable CA1062 // Validate arguments of public methods

namespace Nalix.Codec.DataFrames.Formatter;

/// <inheritdoc/>
public sealed class SessionResumeFormatter : IFillableFormatter<SessionResume> //[cite: 11, 14]
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Serialize(ref DataWriter writer, SessionResume value) //[cite: 11, 14]
    {
        if (value is null)
        {
            return;
        }

        writer.Expand(SessionResume.Size); //[cite: 14]

        // --- Header Section ---
        writer.WriteUnmanaged(((IPacket)value).Header);

        // --- Payload Section ---
        writer.WriteEnum(value.Stage);          //[cite: 14]
        writer.Write(value.SessionToken);       //[cite: 14]
        writer.WriteEnum(value.Reason);         //[cite: 14]
        writer.WriteUnmanaged(value.Proof);     //[cite: 14]
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SessionResume Deserialize(ref DataReader reader) //[cite: 11, 14]
    {
        SessionResume packet = SessionResume.Create();
        this.Fill(ref reader, packet); //[cite: 11]
        return packet;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Fill(ref DataReader reader, SessionResume value) //[cite: 11, 14]
    {
        // --- Header Section ---
        ((IPacket)value).Header = reader.ReadUnmanaged<PacketHeader>();

        // --- Payload Section ---
        value.Stage = reader.ReadEnumByte<SessionResumeStage>();   //[cite: 14]
        value.SessionToken = reader.ReadUInt64();                  //[cite: 14]
        value.Reason = reader.ReadEnumUInt16<ProtocolReason>();    //[cite: 14]
        value.Proof = reader.ReadUnmanaged<Bytes32>();             //[cite: 14]
    }
}
