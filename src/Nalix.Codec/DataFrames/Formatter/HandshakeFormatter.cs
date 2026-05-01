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
public sealed class HandshakeFormatter : IFillableFormatter<Handshake> //[cite: 11, 13]
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Serialize(ref DataWriter writer, Handshake value) //[cite: 11, 13]
    {
        if (value is null)
        {
            return;
        }

        writer.Expand(Handshake.Size); //[cite: 13]

        // --- Header Section ---
        writer.Write(value.MagicNumber);
        writer.Write(value.OpCode);
        writer.Write((byte)value.Flags);
        writer.Write((byte)value.Priority);
        writer.Write(value.SequenceId);

        // --- Payload Section ---
        writer.WriteEnum(value.Stage);                   //[cite: 13]
        writer.WriteEnum(value.Reason);                  //[cite: 13]
        writer.Write(value.SessionToken);                //[cite: 13]
        writer.WriteUnmanaged(value.PublicKey);          //[cite: 13]
        writer.WriteUnmanaged(value.Nonce);              //[cite: 13]
        writer.WriteUnmanaged(value.Proof);              //[cite: 13]
        writer.WriteUnmanaged(value.TranscriptHash);     //[cite: 13]
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Handshake Deserialize(ref DataReader reader) //[cite: 11, 13]
    {
        Handshake packet = Handshake.Create();
        this.Fill(ref reader, packet); //[cite: 11]
        return packet;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Fill(ref DataReader reader, Handshake value) //[cite: 11, 13]
    {
        // --- Header Section ---
        value.MagicNumber = reader.ReadUInt32();
        value.OpCode = reader.ReadUInt16();
        value.Flags = (PacketFlags)reader.ReadByte();
        value.Priority = (PacketPriority)reader.ReadByte();
        value.SequenceId = reader.ReadUInt16();

        // --- Payload Section ---
        value.Stage = reader.ReadEnumByte<HandshakeStage>();           //[cite: 13]
        value.Reason = reader.ReadEnumUInt16<ProtocolReason>();        //[cite: 13]
        value.SessionToken = reader.ReadUInt64();                      //[cite: 13]
        value.PublicKey = reader.ReadUnmanaged<Bytes32>();             //[cite: 13]
        value.Nonce = reader.ReadUnmanaged<Bytes32>();                 //[cite: 13]
        value.Proof = reader.ReadUnmanaged<Bytes32>();                 //[cite: 13]
        value.TranscriptHash = reader.ReadUnmanaged<Bytes32>();        //[cite: 13]
    }
}
