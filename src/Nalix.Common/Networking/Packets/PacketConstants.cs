// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking.Protocols;

namespace Nalix.Common.Networking.Packets;

/// <summary>
/// Defines default values and constants for packet configuration and memory thresholds.
/// </summary>
public static class PacketConstants
{
    /// <summary>
    /// The size, in bytes, of the fixed packet header.
    /// Includes:
    /// <list type="bullet">
    /// <item><see cref="PacketFlags"/>Flags (Byte)</item>
    /// <item><see cref="ushort"/> OpCode (UInt32)</item>
    /// <item><see cref="ushort"/> Length (UInt16)</item>
    /// <item><see cref="uint"/> MagicNumber</item>
    /// <item><see cref="PacketPriority"/>Priority (Byte)</item>
    /// <item><see cref="ProtocolType"/>Protocol (Byte)</item>
    /// </list>
    /// </summary>
    public const byte HeaderSize =
        sizeof(uint) +  // MagicNumber = 4  (offset 0)
        sizeof(ushort) +  // OpCode      = 2  (offset 4)
        sizeof(byte) +    // Flags       = 1  (offset 6)
        sizeof(byte) +    // Priority    = 1  (offset 7)
        sizeof(byte);     // Protocol    = 1  (offset 8)

    /// <summary>
    /// The default operation code value for packets.
    /// </summary>
    public const byte OpcodeDefault = 0x00;

    /// <summary>
    /// The threshold size, in bytes, above which memory is allocated from the heap instead of the stack.
    /// </summary>
    public const short HeapAllocLimit = 0x0400;

    /// <summary>
    /// The maximum size, in bytes, eligible for stack allocation.
    /// </summary>
    public const short StackAllocLimit = 0x0200;

    /// <summary>
    /// The minimum payload size, in bytes, required to enable compression.
    /// Payloads smaller than this value will not be compressed.
    /// </summary>
    public const short CompressionThreshold = 0x0100;

    /// <summary>
    /// The maximum allowed total packet size, in bytes.
    /// This limit is 65,535 bytes (0xFFFF), corresponding to the maximum value of an unsigned 16-bit integer.
    /// </summary>
    public const ushort PacketSizeLimit = 0xFFFF;
}
