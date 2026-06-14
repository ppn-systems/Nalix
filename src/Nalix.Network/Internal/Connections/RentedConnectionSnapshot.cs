// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Nalix.Abstractions.Networking;

namespace Nalix.Network.Internal.Connections;

/// <summary>
/// A zero-allocation snapshot of connections rented from <see cref="ArrayPool{T}"/>.
/// Disposing this struct returns the rented buffer back to the pool.
/// </summary>
[DebuggerNonUserCode]
[SkipLocalsInit]
internal readonly struct RentedConnectionSnapshot : IDisposable
{
    private readonly IConnection[] _buffer;

    /// <summary>
    /// Gets the number of valid connections in the snapshot.
    /// </summary>
    public int Count { get; }

    /// <summary>
    /// Gets a read-only span over the valid connections.
    /// </summary>
    public ReadOnlySpan<IConnection> Span
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(_buffer, 0, Count);
    }

    /// <summary>
    /// Gets the connection at the specified index.
    /// </summary>
    public IConnection this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _buffer[index];
    }

    /// <summary>
    /// Gets a value indicating whether this snapshot is empty.
    /// </summary>
    public bool IsEmpty
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Count == 0;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RentedConnectionSnapshot"/> struct.
    /// </summary>
    /// <param name="buffer">The rented buffer from <see cref="ArrayPool{T}"/>.</param>
    /// <param name="count">The number of valid entries in the buffer.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RentedConnectionSnapshot(IConnection[] buffer, int count)
    {
        _buffer = buffer;
        Count = count;
    }

    /// <summary>
    /// Returns the rented buffer back to the shared <see cref="ArrayPool{T}"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (_buffer is not null)
        {
            ArrayPool<IConnection>.Shared.Return(_buffer, clearArray: true);
        }
    }
}
