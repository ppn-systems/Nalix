// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

/* 
 * PERFORMANCE NOTE:
 * This formatter is pre-registered in the static constructor of the Control class 
 * to eliminate runtime lookups and bypass dynamic delegate loops. 
 * This ensures maximum throughput and zero-allocation during serialization.
 */

using System.Runtime.CompilerServices;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Abstractions.Primitives;
using Nalix.Codec.DataFrames.SignalFrames;
using Nalix.Codec.Extensions;
using Nalix.Codec.Memory;
using Nalix.Codec.Serialization;

#pragma warning disable CA1062 // Validate arguments of public methods

namespace Nalix.Codec.DataFrames.Formatter;

/// <inheritdoc/>
public sealed class DirectiveFormatter : IFillableFormatter<Directive> //[cite: 11, 12]
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Serialize(ref DataWriter writer, in Directive value) //[cite: 11, 12]
    {
        if (value is null)
        {
            return;
        }

        writer.Expand(Directive.Size); //[cite: 12]

        // --- Header Section ---
        writer.WriteUnmanaged(value.Header);

        // --- Payload Section ---
        writer.WriteEnum(value.Type);     //[cite: 12]
        writer.WriteEnum(value.Reason);   //[cite: 12]
        writer.WriteEnum(value.Action);   //[cite: 12]
        writer.WriteEnum(value.Control);  //[cite: 12]
        writer.Write(value.Arg0);         //[cite: 12]
        writer.Write(value.Arg1);         //[cite: 12]
        writer.Write(value.Arg2);         //[cite: 12]
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Directive Deserialize(ref DataReader reader) //[cite: 11, 12]
    {
        Directive packet = Directive.Create();
        this.Fill(ref reader, packet); //[cite: 11]
        return packet;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Fill(ref DataReader reader, Directive value) //[cite: 11, 12]
    {
        // --- Header Section ---
        value.Header = reader.ReadUnmanaged<PacketHeader>();

        // --- Payload Section ---
        value.Type = reader.ReadEnumByte<ControlType>();            //[cite: 12]
        value.Reason = reader.ReadEnumUInt16<ProtocolReason>();     //[cite: 12]
        value.Action = reader.ReadEnumByte<ProtocolAdvice>();       //[cite: 12]
        value.Control = reader.ReadEnumByte<ControlFlags>();        //[cite: 12]
        value.Arg0 = reader.ReadUInt32();                           //[cite: 12]
        value.Arg1 = reader.ReadUInt32();                           //[cite: 12]
        value.Arg2 = reader.ReadUInt16();                           //[cite: 12]
    }
}
