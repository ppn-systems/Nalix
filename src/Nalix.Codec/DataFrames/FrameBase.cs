// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Abstractions;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Primitives;
using Nalix.Abstractions.Serialization;

namespace Nalix.Codec.DataFrames;

/// <summary>
/// Provides a base implementation for all frames within the Nalix system.
/// This class handles the standard 10-byte header and exposes it for manipulation.
/// </summary>
public abstract class FrameBase : IPacket, IPacketHeader
{
    /// <summary>
    /// Gets the total length of the serialized packet in bytes, including header and content.
    /// </summary>
    [SerializeIgnore] public abstract int Length { get; }

    /// <inheritdoc/>
    [SkipCleanAttribute]
    [SerializeHeader(0)]
    private PacketHeader _header;

    /// <inheritdoc/>
    [SkipClean]
    [SerializeIgnore]
    [SerializeHeader(0)]
    public PacketHeader Header { get => _header; set => _header = value; }

    // --- IPacketHeader: direct field access, zero-copy ---

    /// <inheritdoc/>
    [SkipClean]
    [SerializeIgnore]
    public uint MagicNumber
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        get => _header.MagicNumber;
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        set => _header.MagicNumber = value;
    }

    /// <inheritdoc/>
    [SkipClean]
    [SerializeIgnore]
    public ushort OpCode
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        get => _header.OpCode;
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        set => _header.OpCode = value;
    }

    /// <inheritdoc/>
    [SkipClean]
    [SerializeIgnore]
    public PacketFlags Flags
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        get => _header.Flags;
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        set => _header.Flags = value;
    }

    /// <inheritdoc/>
    [SkipClean]
    [SerializeIgnore]
    public PacketPriority Priority
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        get => _header.Priority;
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        set => _header.Priority = value;
    }

    /// <inheritdoc/>
    [SkipClean]
    [SerializeIgnore]
    public ushort SequenceId
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        get => _header.SequenceId;
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        set => _header.SequenceId = value;
    }

    #region APIs

    /// <inheritdoc/>
    public abstract byte[] Serialize();

    /// <inheritdoc/>
    public abstract int Serialize(Span<byte> buffer);

    /// <inheritdoc/>
    public abstract void ResetForPool();

    #endregion APIs
}
