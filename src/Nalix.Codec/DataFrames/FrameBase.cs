// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.CodeAnalysis;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Primitives;
using Nalix.Abstractions.Serialization;

namespace Nalix.Codec.DataFrames;

/// <summary>
/// Represents the base class for all packet frames in the messaging system.
/// Provides Abstractions header fields and serialization logic for derived packet types.
/// </summary>
[ExcludeFromCodeCoverage]
[SerializePackable(SerializeLayout.Explicit)]
public abstract class FrameBase : IPacket, IPacketHeader
{
    /// <summary>
    /// Gets the total length of the serialized packet in bytes, including header and content.
    /// </summary>
    [SerializeIgnore] public abstract int Length { get; }

    /// <inheritdoc/>
    [SerializeHeader(0)] private PacketHeader _header;

    /// <inheritdoc/>
    [SerializeIgnore]
    public PacketHeader Header { get => _header; set => _header = value; }

    // --- IPacketHeaderAccessor: direct field access, zero-copy ---

    /// <inheritdoc/>
    [SerializeIgnore]
    public uint MagicNumber
    {
        get => _header.MagicNumber;
        set => _header.MagicNumber = value;
    }

    /// <inheritdoc/>
    [SerializeIgnore]
    public ushort OpCode
    {
        get => _header.OpCode;
        set => _header.OpCode = value;
    }

    /// <inheritdoc/>
    [SerializeIgnore]
    public PacketFlags Flags
    {
        get => _header.Flags;
        set => _header.Flags = value;
    }

    /// <inheritdoc/>
    [SerializeIgnore]
    public PacketPriority Priority
    {
        get => _header.Priority;
        set => _header.Priority = value;
    }

    /// <inheritdoc/>
    [SerializeIgnore]
    public ushort SequenceId
    {
        get => _header.SequenceId;
        set => _header.SequenceId = value;
    }

    /// <inheritdoc/>
    public abstract void ResetForPool();

    /// <inheritdoc/>
    public abstract byte[] Serialize();

    /// <inheritdoc/>
    public abstract int Serialize(Span<byte> buffer);
}
