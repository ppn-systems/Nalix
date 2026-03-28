// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.ComponentModel;
using Nalix.Framework.Memory.Internal;

namespace Nalix.Framework.LZ4.Engine;

/// <summary>
/// Provides pooled hash tables for LZ4 compression to avoid repeated allocations.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
internal static class LZ4HashTablePool
{
    #region Fields

    [ThreadStatic]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "<Pending>")]
    private static int[]? t_hashTable;
    private static readonly ArrayPool<int> s_pool = ArrayPool<int>.Shared;

    #endregion Fields

    #region APIs

    /// <summary>
    /// Gets a thread-local hash table for compression operations.
    /// The hash table is automatically cleared before returning.
    /// </summary>
    /// <returns>A cleared hash table ready for use.</returns>
    public static int[] Rent()
    {
        int size = MatchFinder.HashTableSize;

        int[]? hashTable = t_hashTable;

        if (hashTable is not null && hashTable.Length == size)
        {
            new Span<int>(hashTable).Clear();
            t_hashTable = null;
            return hashTable;
        }

        hashTable = s_pool.Rent(size);

        if (hashTable.Length != size)
        {
            int[] resized = new int[size];
            s_pool.Return(hashTable);
            hashTable = resized;
        }

        new Span<int>(hashTable).Clear();
        return hashTable;
    }

    /// <summary>
    /// Returns a hash table to the pool (no-op for thread-static storage).
    /// </summary>
    /// <param name="hashTable"></param>
    public static void Return(int[] hashTable)
    {
        if (hashTable is null)
        {
            return;
        }

        int size = MatchFinder.HashTableSize;

        // Ưu tiên giữ lại cho thread hiện tại
        if (hashTable.Length == size && t_hashTable is null)
        {
            t_hashTable = hashTable;
            return;
        }

        // Trả về pool nếu không dùng thread-local
        s_pool.Return(hashTable);
    }

    /// <summary>
    /// Clears the thread-local hash table cache (useful for testing or memory cleanup).
    /// </summary>
    public static void Clear()
    {
        if (t_hashTable is not null)
        {
            s_pool.Return(t_hashTable);
            t_hashTable = null;
        }
    }

    #endregion APIs
}
