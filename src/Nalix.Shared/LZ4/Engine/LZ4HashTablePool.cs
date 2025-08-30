// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Shared.Memory.Internal;

namespace Nalix.Shared.LZ4.Engine;

/// <summary>
/// Provides pooled hash tables for LZ4 compression to avoid repeated allocations.
/// </summary>
public static class LZ4HashTablePool
{
    [System.ThreadStatic]
    private static System.Int32[]? t_hashTable;

    /// <summary>
    /// Gets a thread-local hash table for compression operations.
    /// The hash table is automatically cleared before returning. 
    /// </summary>
    /// <returns>A cleared hash table ready for use.</returns>
    public static System.Int32[] Rent()
    {
        System.Int32[]? hashTable = t_hashTable;

        if (hashTable is null || hashTable.Length != MatchFinder.HashTableSize)
        {
            hashTable = new System.Int32[MatchFinder.HashTableSize];
            t_hashTable = hashTable;
        }

        new System.Span<System.Int32>(hashTable).Clear();

        return hashTable;
    }

    /// <summary>
    /// Returns a hash table to the pool (no-op for thread-static storage).
    /// </summary>
    public static void Return(System.Int32[] _)
    {
        // No-op for thread-static, but included for API consistency
    }

    /// <summary>
    /// Clears the thread-local hash table cache (useful for testing or memory cleanup).
    /// </summary>
    public static void Clear() => t_hashTable = null;
}
