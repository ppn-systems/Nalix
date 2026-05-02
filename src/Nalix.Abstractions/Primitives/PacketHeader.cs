// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.InteropServices;
using Nalix.Abstractions.Networking.Packets;

namespace Nalix.Abstractions.Primitives;

/// <summary>
/// Represents the standard 10-byte header of a Nalix packet.
/// Absolute field positions via <see cref="FieldOffsetAttribute"/> guarantee
/// wire-compatible layout across all platforms.
/// </summary>
[StructLayout(LayoutKind.Explicit, Pack = 1)]
public struct PacketHeader
{
    /// <summary>
    /// The wire size of the header in bytes.
    /// </summary>
    public const int Size = (int)PacketHeaderOffset.Region;

    /// <summary>
    /// Gets the magic number that identifies the packet format or protocol.
    /// </summary>
    [FieldOffset((int)PacketHeaderOffset.MagicNumber)] public uint MagicNumber;

    /// <summary>
    /// Gets the operation code that identifies the packet handler.
    /// </summary>
    [FieldOffset((int)PacketHeaderOffset.OpCode)] public ushort OpCode;

    /// <summary>
    /// Gets the flags associated with the packet.
    /// </summary>
    [FieldOffset((int)PacketHeaderOffset.Flags)] public PacketFlags Flags;

    /// <summary>
    /// Gets the priority level of the packet.
    /// </summary>
    [FieldOffset((int)PacketHeaderOffset.Priority)] public PacketPriority Priority;

    /// <summary>
    /// Gets the sequence identifier used to correlate requests and responses.
    /// </summary>
    [FieldOffset((int)PacketHeaderOffset.SequenceId)] public ushort SequenceId;
}
