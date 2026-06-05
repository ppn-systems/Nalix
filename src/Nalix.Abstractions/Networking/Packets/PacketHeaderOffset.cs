// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.


namespace Nalix.Abstractions.Networking.Packets;

/// <summary>
/// Represents the positions of fields in the serialization order.
/// Each value corresponds to a specific position in the serialized data.
/// </summary>
public enum PacketHeaderOffset
{
    /// <summary>
    /// Represents the operation code (OpCode) field, specifying the command or type of the packet.
    /// This field comes first in the serialized data.
    /// </summary>
    OpCode = 0,

    /// <summary>
    /// Represents the flags field, which contains state or processing options for the packet.
    /// This field comes second in the serialized data.
    /// </summary>
    Flags = OpCode + sizeof(ushort),

    /// <summary>
    /// Represents the priority field, indicating the processing priority of the packet.
    /// This field comes third in the serialized data.
    /// </summary>
    Priority = Flags + sizeof(byte),

    /// <summary>
    /// SequenceId field: Used for packet sequence correlation.
    /// </summary>
    SequenceId = Priority + sizeof(byte),

    /// <summary>
    /// Represents the exclusive end offset of the packet header fields in the serialized data.
    /// This value equals the total header size (6 bytes).
    /// </summary>
    Region = SequenceId + sizeof(ushort),

    /// <inheritdoc/>
    MaxValue = byte.MaxValue,
}
