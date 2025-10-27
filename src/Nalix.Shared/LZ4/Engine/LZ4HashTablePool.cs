// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using Nalix.Shared.Memory.Internal;

namespace Nalix.Shared.LZ4.Engine;

/// <summary>
/// Provides pooled hash tables for LZ4 compression to avoid repeated allocations.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
internal static class LZ4HashTablePool
{
    #region Fields

    [ThreadStatic]
    private static int[]? t_hashTable;

    #endregion Fields

    #region APIs

    /// <summary>
    /// Gets a thread-local hash table for compression operations.
    /// The hash table is automatically cleared before returning.
    /// </summary>
    /// <returns>A cleared hash table ready for use.</returns>
    public static int[] Rent()
    {
        int[]? hashTable = t_hashTable;

        if (hashTable is null || hashTable.Length != MatchFinder.HashTableSize)
        {
            hashTable = new int[MatchFinder.HashTableSize];
            t_hashTable = hashTable;
        }

        new Span<int>(hashTable).Clear();

        return hashTable;
    }

    /// <summary>
    /// Returns a hash table to the pool (no-op for thread-static storage).
    /// </summary>
    /// <param name="_"></param>
    public static void Return(int[] _)
    {
        // No-op for thread-static, but included for API consistency
    }

    /// <summary>
    /// Clears the thread-local hash table cache (useful for testing or memory cleanup).
    /// </summary>
    public static void Clear() => t_hashTable = null;

    #endregion APIs
}
