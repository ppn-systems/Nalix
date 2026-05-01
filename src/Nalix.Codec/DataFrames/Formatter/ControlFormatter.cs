// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Codec.DataFrames.SignalFrames;
using Nalix.Codec.Extensions;
using Nalix.Codec.Memory;
using Nalix.Codec.Serialization;

#pragma warning disable CA1062 // Validate arguments of public methods

namespace Nalix.Codec.DataFrames.Formatter;

/// <summary>
/// Custom zero-allocation formatter for the Control packet.
/// Bypasses all dynamic delegate loops for maximum throughput.
/// </summary>
public sealed class ControlFormatter : IFillableFormatter<Control> //[cite: 11]
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Serialize(ref DataWriter writer, Control value) //[cite: 11]
    {
        if (value is null)
        {
            return;
        }

        writer.Expand(Control.Size);

        // --- Header Section (10 bytes) ---
        writer.Write(value.MagicNumber);    //[cite: 2, 5]
        writer.Write(value.OpCode);         //[cite: 2, 5]
        writer.Write((byte)value.Flags);    //[cite: 2, 5]
        writer.Write((byte)value.Priority); //[cite: 2, 5]
        writer.Write(value.SequenceId);     //[cite: 2, 5]

        // --- Payload Section ---
        writer.WriteEnum(value.Reason);     //[cite: 3, 5]
        writer.WriteEnum(value.Type);       //[cite: 3, 5]
        writer.Write(value.Timestamp);      //[cite: 3, 5]
        writer.Write(value.MonoTicks);      //[cite: 3, 5]
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Control Deserialize(ref DataReader reader) //[cite: 11]
    {
        Control packet = Control.Create();
        this.Fill(ref reader, packet); //[cite: 11]
        return packet;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Fill(ref DataReader reader, Control value) //[cite: 11]
    {
        // --- Header Section ---
        value.MagicNumber = reader.ReadUInt32();                //[cite: 2, 4]
        value.OpCode = reader.ReadUInt16();                     //[cite: 2, 4]
        value.Flags = (PacketFlags)reader.ReadByte();           //[cite: 2, 4]
        value.Priority = (PacketPriority)reader.ReadByte();     //[cite: 2, 4]
        value.SequenceId = reader.ReadUInt16();                 //[cite: 2, 4]

        // --- Payload Section ---
        value.Reason = reader.ReadEnumUInt16<ProtocolReason>(); //[cite: 3, 4]
        value.Type = reader.ReadEnumByte<ControlType>();        //[cite: 3, 4]
        value.Timestamp = reader.ReadInt64();                   //[cite: 3, 4]
        value.MonoTicks = reader.ReadInt64();                   //[cite: 3, 4]
    }
}
