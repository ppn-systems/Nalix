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
public abstract class FrameBase : IPacket
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

    // --- Facade Properties ---
    // These properties modify the Header struct directly without copying it.

    /// <summary>Gets or sets the magic number.</summary>
    [SerializeIgnore]
    public uint MagicNumber
    {
        get => this.Header.MagicNumber;
        set => _header.MagicNumber = value;
    }

    /// <summary>Gets or sets the operation code.</summary>
    [SerializeIgnore]
    public ushort OpCode
    {
        get => this.Header.OpCode;
        set => _header.OpCode = value;
    }

    /// <summary>Gets or sets the packet flags.</summary>
    [SerializeIgnore]
    public PacketFlags Flags
    {
        get => this.Header.Flags;
        set => _header.Flags = value;
    }

    /// <summary>Gets or sets the priority.</summary>
    [SerializeIgnore]
    public PacketPriority Priority
    {
        get => this.Header.Priority;
        set => _header.Priority = value;
    }

    /// <summary>Gets or sets the sequence identifier.</summary>
    [SerializeIgnore]
    public ushort SequenceId
    {
        get => this.Header.SequenceId;
        set => _header.SequenceId = value;
    }

    /// <inheritdoc/>
    public abstract void ResetForPool();

    /// <inheritdoc/>
    public abstract byte[] Serialize();

    /// <inheritdoc/>
    public abstract int Serialize(Span<byte> buffer);
}
