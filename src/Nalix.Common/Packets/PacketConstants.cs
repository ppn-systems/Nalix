// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Packets.Enums;
using Nalix.Common.Protocols;

namespace Nalix.Common.Packets;

/// <summary>
/// Defines default values and constants for packet configuration and memory thresholds.
/// </summary>
public static class PacketConstants
{
    /// <summary>
    /// The size, in bytes, of the fixed packet header.
    /// Includes:
    /// <list type="bullet">
    /// <item><see cref="PacketFlags"/></item>
    /// <item><see cref="System.UInt16"/> OpCode</item>
    /// <item><see cref="System.UInt16"/> Length</item>
    /// <item><see cref="System.UInt32"/> MagicNumber</item>
    /// <item><see cref="PacketPriority"/></item>
    /// <item><see cref="TransportProtocol"/></item>
    /// </list>
    /// </summary>
    public const System.Byte HeaderSize =
        sizeof(PacketFlags) +
        sizeof(System.UInt16) +  // OpCode
        sizeof(System.UInt16) +  // Length
        sizeof(System.UInt32) +  // MagicNumber
        sizeof(PacketPriority) +
        sizeof(TransportProtocol);

    /// <summary>
    /// The default operation code value for packets.
    /// </summary>
    public const System.Byte OpCodeDefault = 0x00;

    /// <summary>
    /// The threshold size, in bytes, above which memory is allocated from the heap instead of the stack.
    /// </summary>
    public const System.Int16 HeapAllocLimit = 0x0400;

    /// <summary>
    /// The maximum size, in bytes, eligible for stack allocation.
    /// </summary>
    public const System.Int16 StackAllocLimit = 0x0200;

    /// <summary>
    /// The minimum payload size, in bytes, required to enable compression.
    /// Payloads smaller than this value will not be compressed.
    /// </summary>
    public const System.Int16 CompressionThreshold = 0x0100;

    /// <summary>
    /// The maximum allowed total packet size, in bytes.
    /// This limit is 65,535 bytes (0xFFFF), corresponding to the maximum value of an unsigned 16-bit integer.
    /// </summary>
    public const System.UInt16 PacketSizeLimit = 0xFFFF;
}
