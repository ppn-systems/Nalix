using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Notio.Shared.Memory.Buffers;

/// <summary>
/// Information about the Buffers Pools with optimized memory layout.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct BufferInfo : IEquatable<BufferInfo>
{
    /// <summary>
    /// Number of misses
    /// </summary>
    public required int Misses { get; init; }

    /// <summary>
    /// Size of the buffer
    /// </summary>
    public required int BufferSize { get; init; }

    /// <summary>
    /// Number of free buffers
    /// </summary>
    public required int FreeBuffers { get; init; }

    /// <summary>
    /// Total Number of buffers
    /// </summary>
    public required int TotalBuffers { get; init; }

    /// <summary>
    /// Gets a value indicating whether the pool can be shrunk
    /// </summary>
    public bool CanShrink => FreeBuffers > (TotalBuffers * 0.5);

    /// <summary>
    /// Gets a value indicating whether the pool needs expansion
    /// </summary>
    public bool NeedsExpansion => FreeBuffers < (TotalBuffers * 0.25);

    /// <summary>
    /// Gets the usage ratio of the buffer pool
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetUsageRatio() => Math.Max(0, Math.Min(1.0, 1.0 - (FreeBuffers / (double)Math.Max(1, TotalBuffers))));

    /// <summary>
    /// Gets the miss rate as a ratio of total buffers
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetMissRate() => Misses / (double)Math.Max(1, TotalBuffers);
}
