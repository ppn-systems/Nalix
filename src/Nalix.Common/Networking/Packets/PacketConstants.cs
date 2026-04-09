// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Common.Networking.Packets;

/// <summary>
/// Defines packet framing and allocation thresholds shared by the networking stack.
/// </summary>
public static class PacketConstants
{
    /// <summary>
    /// The size, in bytes, of the fixed packet header.
    /// </summary>
    public const byte HeaderSize =
        sizeof(uint) +    // MagicNumber = 4  (offset 0)
        sizeof(ushort) +  // OpCode      = 2  (offset 4)
        sizeof(byte) +    // Flags       = 1  (offset 6)
        sizeof(byte) +    // Priority    = 1  (offset 7)
        sizeof(byte) +    // Protocol    = 1  (offset 8)
        sizeof(uint);     // SequenceId  = 4  (offset 9)
                          // Total header size = 12 bytes (offsets 0-11)

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
    /// </summary>
    public const short CompressionThreshold = 0x0100;

    /// <summary>
    /// The maximum allowed total packet size, in bytes.
    /// </summary>
    public const int PacketSizeLimit = int.MaxValue - HeaderSize;
}
