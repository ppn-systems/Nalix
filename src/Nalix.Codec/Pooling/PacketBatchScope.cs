// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Codec.DataFrames;

namespace Nalix.Codec.Pooling;

/// <summary>
/// A zero-allocation wrapper that ensures a batch of rented packets and their underlying array are returned to their pools upon disposal.
/// </summary>
/// <typeparam name="TPacket">The packet type.</typeparam>
public readonly struct PacketBatchScope<TPacket> : IDisposable where TPacket : PacketBase<TPacket>, IPacketStaticOpcode, new()
{
    private readonly TPacket[] _buffer;
    private readonly int _count;

    /// <summary>
    /// Initializes a new lease for a batch of packets.
    /// </summary>
    /// <param name="count">The number of packets to rent.</param>
    public PacketBatchScope(int count)
    {
        _count = count;
        _buffer = ArrayPool<TPacket>.Shared.Rent(count);
        for (int i = 0; i < count; i++)
        {
            _buffer[i] = PacketBase<TPacket>.Create();
        }
    }

    /// <summary>
    /// Gets the number of valid packets in this batch.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Gets the packet at the specified index.
    /// </summary>
    public TPacket this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint)index >= (uint)_count)
            {
                throw new IndexOutOfRangeException();
            }
            return _buffer[index];
        }
    }

    /// <summary>
    /// Gets a <see cref="Span{T}"/> over the rented packets.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<TPacket> AsSpan() => new Span<TPacket>(_buffer, 0, _count);

    /// <summary>
    /// Gets a <see cref="ReadOnlySpan{T}"/> over the rented packets.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<TPacket> AsReadOnlySpan() => new ReadOnlySpan<TPacket>(_buffer, 0, _count);

    /// <summary>
    /// Returns the packets to their pool and the array to the ArrayPool.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (_buffer == null)
            return;

        for (int i = 0; i < _count; i++)
        {
            _buffer[i]?.Dispose();
            _buffer[i] = null!; // Clear reference to prevent memory leaks in ArrayPool
        }
        
        ArrayPool<TPacket>.Shared.Return(_buffer, clearArray: false);
    }
}
