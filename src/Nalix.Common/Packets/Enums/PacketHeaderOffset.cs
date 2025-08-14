// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Attributes;

namespace Nalix.Common.Packets.Enums;

/// <summary>
/// Represents the positions of fields in the serialization order.
/// Each value corresponds to a specific position in the serialized data.
/// </summary>
public enum PacketHeaderOffset : System.Byte
{
    /// <summary>
    /// Represents the magic number field, which uniquely identifies the packet format or protocol.
    /// This field comes first in the serialized data.
    /// </summary>
    [DataType(typeof(System.UInt32))]
    MagicNumber = 0,

    /// <summary>
    /// Represents the operation code (OpCode) field, specifying the command or type of the packet.
    /// This field comes second in the serialized data.
    /// </summary>
    [DataType(typeof(System.UInt16))]
    OpCode = MagicNumber + sizeof(System.UInt32),

    /// <summary>
    /// Represents the flags field, which contains state or processing options for the packet.
    /// This field comes third in the serialized data.
    /// </summary>
    [DataType(typeof(System.Byte))]
    Flags = OpCode + sizeof(System.UInt16),

    /// <summary>
    /// Represents the priority field, indicating the processing priority of the packet.
    /// This field comes fourth in the serialized data.
    /// </summary>
    [DataType(typeof(System.Byte))]
    Priority = Flags + sizeof(System.Byte),

    /// <summary>
    /// Represents the transport protocol field, indicating the transport protocol (e.g., TCP or UDP) used.
    /// This field comes fifth in the serialized data.
    /// </summary>
    [DataType(typeof(System.Byte))]
    Transport = Priority + sizeof(System.Byte),
}
