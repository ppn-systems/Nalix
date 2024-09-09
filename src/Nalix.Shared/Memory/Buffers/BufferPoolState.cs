// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Shared.Memory.Buffers;

/// <summary>
/// Information about the buffer pool with optimized memory layout.
/// </summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
[System.Diagnostics.DebuggerDisplay("Size={BufferSize}, Total={TotalBuffers}, Free={FreeBuffers}, Misses={Misses}")]
public readonly record struct BufferPoolState : System.IEquatable<BufferPoolState>
{
    /// <summary>Number of misses.</summary>
    public required System.Int32 Misses { get; init; }

    /// <summary>Size of each buffer in bytes.</summary>
    public required System.Int32 BufferSize { get; init; }

    /// <summary>Number of free buffers.</summary>
    public required System.Int32 FreeBuffers { get; init; }

    /// <summary>Total number of buffers.</summary>
    public required System.Int32 TotalBuffers { get; init; }

    /// <summary>Gets a value indicating whether the pool can be shrunk (free &gt;= 50%).</summary>
    public System.Boolean CanShrink => FreeBuffers > (TotalBuffers * 0.5);

    /// <summary>Gets a value indicating whether the pool likely needs expansion (free &lt;= 25%).</summary>
    public System.Boolean NeedsExpansion => FreeBuffers < (TotalBuffers * 0.25);

    /// <summary>Gets the usage ratio of the buffer pool.</summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Double GetUsageRatio()
        => System.Math.Max(0, System.Math.Min(1.0, 1.0 - (FreeBuffers / (System.Double)System.Math.Max(1, TotalBuffers))));

    /// <summary>Gets the miss rate as a ratio of total buffers.</summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Double GetMissRate() => Misses / (System.Double)System.Math.Max(1, TotalBuffers);
}