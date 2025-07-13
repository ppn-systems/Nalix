namespace Nalix.Shared.Memory.Buffers;

/// <summary>
/// Information about the Buffers Pools with optimized memory layout.
/// </summary>
[System.Runtime.InteropServices.StructLayout(
    System.Runtime.InteropServices.LayoutKind.Sequential)]
public readonly record struct BufferPoolSnapshot : System.IEquatable<BufferPoolSnapshot>
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public double GetUsageRatio()
        => System.Math.Max(0, System.Math.Min(1.0, 1.0 - (FreeBuffers / (double)System.Math.Max(1, TotalBuffers))));

    /// <summary>
    /// Gets the miss rate as a ratio of total buffers
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public double GetMissRate() => Misses / (double)System.Math.Max(1, TotalBuffers);
}