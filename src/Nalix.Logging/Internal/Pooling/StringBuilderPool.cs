// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Logging.Internal.Pooling;

/// <summary>
/// High-performance StringBuilder pool optimized for logging operations.
/// Provides thread-safe pooling with minimal contention and efficient capacity management.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
internal static class StringBuilderPool
{
    #region Constants

    private const System.Int32 DefaultCapacity = 512;
    private const System.Int32 MaxCapacity = 4096;
    private const System.Int32 PoolSize = 32;

    #endregion Constants

    #region Fields

    [System.ThreadStatic]
    private static System.Text.StringBuilder? t_cachedInstance;

    private static readonly System.Collections.Concurrent.ConcurrentBag<System.Text.StringBuilder> s_pool = [];

    #endregion Fields

    #region Public Methods

    /// <summary>
    /// Rents a StringBuilder from the pool with the specified initial capacity.
    /// </summary>
    /// <param name="capacity">The initial capacity of the StringBuilder.</param>
    /// <returns>A StringBuilder instance from the pool or a new instance.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Text.StringBuilder Rent(System.Int32 capacity = DefaultCapacity)
    {
        // Fast path: thread-local cache
        System.Text.StringBuilder? sb = t_cachedInstance;
        if (sb != null)
        {
            t_cachedInstance = null;

            // Ensure the capacity is appropriate
            if (sb.Capacity < capacity)
            {
                _ = sb.EnsureCapacity(capacity);
            }

            return sb;
        }

        // Try to get from shared pool
        if (s_pool.TryTake(out sb))
        {
            _ = sb.Clear();

            if (sb.Capacity < capacity)
            {
                _ = sb.EnsureCapacity(capacity);
            }

            return sb;
        }

        // Create new instance
        return new System.Text.StringBuilder(capacity);
    }

    /// <summary>
    /// Returns a StringBuilder to the pool for reuse.
    /// </summary>
    /// <param name="builder">The StringBuilder to return to the pool.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Return(System.Text.StringBuilder? builder)
    {
        if (builder == null)
        {
            return;
        }

        // Don't pool if too large to avoid memory bloat
        if (builder.Capacity > MaxCapacity)
        {
            return;
        }

        _ = builder.Clear();

        // Fast path: thread-local cache
        if (t_cachedInstance == null)
        {
            t_cachedInstance = builder;
            return;
        }

        // Return to shared pool if not full
        if (s_pool.Count < PoolSize)
        {
            s_pool.Add(builder);
        }
    }

    #endregion Public Methods
}
