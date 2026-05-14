// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Nalix.Abstractions.Primitives;

namespace Nalix.Abstractions.Networking.Packets;

/// <summary>
/// A high-performance, zero-allocation struct that holds a raw memory payload
/// and its associated packet header, bypassing deserialization.
/// </summary>
/// <remarks>
/// Handlers can define their first parameter as <see cref="ReadOnlyMemory{T}"/> of byte
/// to directly receive the raw memory from this packet struct.
/// </remarks>
[StructLayout(LayoutKind.Auto)]
[DebuggerDisplay("Size = {Memory.Length} bytes, OpCode = {Header.OpCode}")]
public readonly record struct MemoryPacket(ReadOnlyMemory<byte> Memory, PacketHeader Header) : IPacket
{
    /// <inheritdoc/>
    public int Length => this.Memory.Length;

    /// <inheritdoc/>
    PacketHeader IPacket.Header
    {
        get => this.Header;
        set => throw new NotSupportedException("MemoryPacket header is read-only.");
    }

    /// <inheritdoc/>
    public byte[] Serialize() => this.Memory.ToArray();

    /// <inheritdoc/>
    public int Serialize(Span<byte> buffer)
    {
        this.Memory.Span.CopyTo(buffer);
        return this.Memory.Length;
    }
}
