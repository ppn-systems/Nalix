// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Shared.LZ4.Encoders;
using Nalix.Shared.Memory.Internal;

namespace Nalix.Shared.LZ4.Engine;

/// <summary>
/// Provides a thread-local hash table for LZ4 compression with zero-clear overhead.
/// </summary>
/// <remarks>
/// <para>
/// Instead of zeroing all <see cref="MatchFinder.HashTableSize"/> entries (~256 KB) on every
/// call, this pool uses a <b>generation counter</b>: each slot stores
/// <c>(generation &lt;&lt; 24) | position</c>. A slot is a miss when its generation tag
/// differs from the current one. Bumping the generation is O(1) — no memset.
/// </para>
/// <para>
/// Generation wraps 255 → 1 (0 is reserved as "never written"). Positions are 24-bit
/// (max 16 MB), well above <see cref="LZ4CompressionConstants.MaxBlockSize"/> (256 KB).
/// </para>
/// </remarks>
public static class LZ4HashTablePool
{
    #region Constants

    private const System.Int32 GenerationShift = 24;
    private const System.Int32 PositionMask = (1 << GenerationShift) - 1; // 0x00FF_FFFF
    private const System.Byte GenerationMin = 1;
    private const System.Byte GenerationMax = System.Byte.MaxValue;

    #endregion Constants

    #region Thread-local state

    [System.ThreadStatic]
    private static System.Int32[]? t_hashTable;

    [System.ThreadStatic]
    private static System.Byte t_generation; // 0 = uninitialised; valid 1–255

    #endregion Thread-local state

    #region Public API

    /// <summary>
    /// Returns the thread-local hash table, logically cleared in O(1) via a generation bump.
    /// </summary>
    public static LZ4HashTable Rent()
    {
        System.Int32[]? table = t_hashTable;

        if (table is null || table.Length != MatchFinder.HashTableSize)
        {
            // First use on this thread — CLR zero-initialises the array.
            table = new System.Int32[MatchFinder.HashTableSize];
            t_hashTable = table;
            t_generation = GenerationMin;
        }
        else
        {
            // O(1) logical clear — no memset.
            System.Byte g = t_generation;
            t_generation = g >= GenerationMax ? GenerationMin : (System.Byte)(g + 1);
        }

        return new LZ4HashTable(table, t_generation);
    }

    /// <summary>No-op — included for API symmetry.</summary>
    public static void Return(LZ4HashTable _) { }

    /// <summary>Drops the thread-local cache (tests / memory pressure).</summary>
    public static void Clear() { t_hashTable = null; t_generation = 0; }

    #endregion Public API

    // ── Inner wrapper ─────────────────────────────────────────────────────────

    /// <summary>
    /// Lightweight wrapper that encodes/decodes the generation tag transparently.
    /// Passed by value — fits in two registers (reference + byte).
    /// </summary>
    public readonly ref struct LZ4HashTable
    {
        private readonly System.Int32 _genMask; // generation << GenerationShift

        internal LZ4HashTable(System.Int32[] table, System.Byte generation)
        {
            this.RawTable = table;
            _genMask = generation << GenerationShift;
        }

        /// <summary>Raw array for callers that need a pinned pointer.</summary>
        public System.Int32[] RawTable { get; }

        /// <summary>
        /// Returns <see langword="true"/> when the slot holds a value from the current generation.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public System.Boolean TryGet(System.Int32 slot, out System.Int32 position)
        {
            System.Int32 raw = this.RawTable[slot];
            if ((raw & ~PositionMask) != _genMask) { position = 0; return false; }
            position = raw & PositionMask;
            return true;
        }

        /// <summary>Writes <paramref name="position"/> tagged with the current generation.</summary>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Set(System.Int32 slot, System.Int32 position) => this.RawTable[slot] = _genMask | (position & PositionMask);
    }
}