// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Nalix.Shared.Memory.Buffers;

/// <summary>
/// Information about the buffer pool with optimized memory layout.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
[StructLayout(LayoutKind.Sequential)]
[DebuggerDisplay("SIZE={BufferSize}, Total={TotalBuffers}, Free={FreeBuffers}, Misses={Misses}")]
public readonly record struct BufferPoolState : IEquatable<BufferPoolState>
{
    /// <summary>
    /// Number of misses.
    /// </summary>
    public required int Misses { get; init; }

    /// <summary>
    /// Size of each buffer in bytes.
    /// </summary>
    public required int BufferSize { get; init; }

    /// <summary>
    /// Number of free buffers.
    /// </summary>
    public required int FreeBuffers { get; init; }

    /// <summary>
    /// Total number of buffers.
    /// </summary>
    public required int TotalBuffers { get; init; }

    /// <summary>
    /// Gets a value indicating whether the pool can be shrunk (free &gt;= 50%).
    /// </summary>
    public bool CanShrink => FreeBuffers > (TotalBuffers * 0.5);

    /// <summary>
    /// Gets a value indicating whether the pool likely needs expansion (free &lt;= 25%).
    /// </summary>
    public bool NeedsExpansion => FreeBuffers < (TotalBuffers * 0.25);

    /// <summary>
    /// Gets the usage ratio of the buffer pool.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetUsageRatio()
        => TotalBuffers <= 0 ? 0.0 : Math.Max(0.0, Math.Min(1.0, 1.0 - (FreeBuffers / (double)TotalBuffers)));

    /// <summary>
    /// Gets the miss rate as a ratio of total buffers.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetMissRate() => TotalBuffers <= 0 ? 0.0 : Math.Min(1.0, Misses / (double)TotalBuffers);
}
