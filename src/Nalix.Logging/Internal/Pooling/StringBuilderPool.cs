// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;

namespace Nalix.Logging.Internal.Pooling;

/// <summary>
/// High-performance StringBuilder pool optimized for logging operations.
/// Provides thread-safe pooling with minimal contention and efficient capacity management.
/// </summary>
[DebuggerNonUserCode]
[ExcludeFromCodeCoverage]
internal static class StringBuilderPool
{
    #region Constants

    private const int DefaultCapacity = 512;
    private const int MaxCapacity = 4096;
    private const int PoolSize = 32;

    #endregion Constants

    #region Fields

    [ThreadStatic]
    private static StringBuilder? t_cachedInstance;

    private static readonly System.Collections.Concurrent.ConcurrentBag<StringBuilder> s_pool = [];

    #endregion Fields

    #region Public Methods

    /// <summary>
    /// Rents a StringBuilder from the pool with the specified initial capacity.
    /// </summary>
    /// <param name="capacity">The initial capacity of the StringBuilder.</param>
    /// <returns>A StringBuilder instance from the pool or a new instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StringBuilder Rent(int capacity = DefaultCapacity)
    {
        // Fast path: thread-local cache
        StringBuilder? sb = t_cachedInstance;
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
        return new StringBuilder(capacity);
    }

    /// <summary>
    /// Returns a StringBuilder to the pool for reuse.
    /// </summary>
    /// <param name="builder">The StringBuilder to return to the pool.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Return(StringBuilder? builder)
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
