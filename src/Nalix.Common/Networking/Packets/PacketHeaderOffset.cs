// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Abstractions;

namespace Nalix.Common.Networking.Packets;

/// <summary>
/// Represents the positions of fields in the serialization order.
/// Each value corresponds to a specific position in the serialized data.
/// </summary>
public enum PacketHeaderOffset
{
    /// <summary>
    /// Represents the magic number field, which uniquely identifies the packet format or protocol.
    /// This field comes first in the serialized data.
    /// </summary>
    [DataType(typeof(int))]
    MagicNumber = 0,

    /// <summary>
    /// Represents the operation code (OpCode) field, specifying the command or type of the packet.
    /// This field comes second in the serialized data.
    /// </summary>
    [DataType(typeof(ushort))]
    OpCode = MagicNumber + sizeof(uint),

    /// <summary>
    /// Represents the flags field, which contains state or processing options for the packet.
    /// This field comes third in the serialized data.
    /// </summary>
    [DataType(typeof(byte))]
    Flags = OpCode + sizeof(ushort),

    /// <summary>
    /// Represents the priority field, indicating the processing priority of the packet.
    /// This field comes fourth in the serialized data.
    /// </summary>
    [DataType(typeof(byte))]
    Priority = Flags + sizeof(byte),

    /// <summary>
    /// SequenceId field: Used for packet sequence correlation.
    /// </summary>
    [DataType(typeof(ushort))]
    SequenceId = Priority + sizeof(byte),

    /// <summary>
    /// Represents the end offset of the packet header fields in the serialized data.
    /// This value is equal to the offset of the last field and can be used to determine the total header size.
    /// </summary>
    Region = SequenceId + sizeof(ushort),

    /// <inheritdoc/>
    MaxValue = byte.MaxValue,
}
