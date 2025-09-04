// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Shared.LZ4.Encoders;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Benchmarks")]
#endif

namespace Nalix.Shared.Memory.Internal;

/// <summary>
/// Provides functionality for finding matches in input data using a hash table.
/// Optimized for high performance in LZ4 compression.
/// </summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
internal static unsafe class MatchFinder
{
    #region Constants

    private const System.Int32 HashTableBits = 16; // 64k entries
    private const System.Int32 HashShift = 32 - HashTableBits;

    /// <summary>
    /// Size of the hash table used for storing offsets of previously seen sequences.
    /// </summary>
    public const System.Int32 HashTableSize = 1 << HashTableBits;

    /// <summary>
    /// Consider using a pool or limiting stackalloc in extreme cases
    /// </summary>
    public const System.Int32 MaxStackallocHashTableSize = HashTableSize * sizeof(System.Int32);

    #endregion Constants

    #region Constructors

    /// <summary>
    /// Represents a found match with an offset and length.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="Match"/> struct.
    /// </remarks>
    internal readonly struct Match(System.Int32 offset, System.Int32 length)
    {
        public readonly System.Int32 Offset = offset; // Match offset relative to current position
        public readonly System.Int32 Length = length; // Length of the match

        /// <summary>
        /// Determines if the match is valid.
        /// A match is considered valid if its length meets the minimum match length.
        /// </summary>
        public System.Boolean Found => Length >= LZ4CompressionConstants.MinMatchLength;
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    /// Finds the longest match for the current input position within the sliding window.
    /// </summary>
    /// <param name="hashTable">HashData table mapping hash values to input offsets.</param>
    /// <param name="inputBase">Pointer to the start of the input buffer.</param>
    /// <param name="currentInputPtr">Pointer to the current position in the input buffer.</param>
    /// <param name="inputLimit">Pointer to the end of the input buffer or match limit.</param>
    /// <param name="searchStartPtr">Pointer to the start of the sliding window for searching.</param>
    /// <param name="currentInputOffset">Offset of the current input pointer relative to the input base.</param>
    /// <returns>A <see cref="Match"/> struct representing the longest match found, or a default value if no match is found.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
    System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static Match FindLongestMatch(
        System.Int32* hashTable,
        System.Byte* inputBase,
        System.Byte* currentInputPtr,
        System.Byte* inputLimit,
        System.Byte* searchStartPtr,
        System.Int32 currentInputOffset)
    {
#if DEBUG
        System.Diagnostics.Debug.Assert(inputBase is not null, "Input base pointer is null");
        System.Diagnostics.Debug.Assert(hashTable is not null, "Hash table pointer is null");
        System.Diagnostics.Debug.Assert(currentInputPtr is not null, "Current input pointer is null");
        System.Diagnostics.Debug.Assert(currentInputPtr >= inputBase, "Current pointer is before base");
        System.Diagnostics.Debug.Assert(inputLimit >= currentInputPtr, "Input limit is before current pointer");
#endif

        // Ensure there are enough bytes to find a match
        if ((System.UIntPtr)(inputLimit - currentInputPtr) < LZ4CompressionConstants.MinMatchLength)
        {
            return default;
        }

        // ✅ FIX: Protect against reading beyond buffer
        if (currentInputPtr + sizeof(System.UInt32) > inputLimit + LZ4CompressionConstants.LastLiteralSize)
        {
            return default;
        }

        // Read the current 4-byte sequence and compute its hash
        System.UInt32 currentSequence = MemOps.ReadUnaligned<System.UInt32>(currentInputPtr);
        System.UInt32 hash;

#if NET5_0_OR_GREATER
        if (System.Runtime.Intrinsics.X86.Sse42.IsSupported)
        {
            hash = System.Runtime.Intrinsics.X86.Sse42.Crc32(0, currentSequence);
        }
        else
#endif
        {
            hash = (currentSequence * 2654435761u) >> HashShift;
        }

        hash &= HashTableSize - 1;

        // Retrieve the candidate match offset and update the hash table
        System.Int32 matchCandidateOffset = hashTable[hash];
        hashTable[hash] = currentInputOffset;

        if (matchCandidateOffset < 0 || matchCandidateOffset >= currentInputOffset)
        {
            return default;
        }

        // Calculate the candidate match pointer
        System.Byte* matchCandidatePtr = inputBase + matchCandidateOffset;

        if (matchCandidatePtr < searchStartPtr)
        {
            return default;
        }

        // Calculate offset first
        System.Int32 offset = (System.Int32)(currentInputPtr - matchCandidatePtr);

        if (offset is <= 0 or > LZ4CompressionConstants.MaxOffset)
        {
            return default;
        }

        // Check if sequences match
        if (MemOps.ReadUnaligned<System.UInt32>(matchCandidatePtr) != currentSequence)
        {
            return default;
        }

        // Calculate the length of the match
        System.Byte* matchEnd = matchCandidatePtr + LZ4CompressionConstants.MinMatchLength;
        System.Byte* currentEnd = currentInputPtr + LZ4CompressionConstants.MinMatchLength;

        System.Int32 maxMatchLen = (System.Int32)(inputLimit - currentEnd);
        if (maxMatchLen < 0)
        {
            maxMatchLen = 0;
        }

        System.Int32 additionalMatchBytes = MemOps.CountEqualBytes(matchEnd, currentEnd, maxMatchLen);
        System.Int32 matchLength = LZ4CompressionConstants.MinMatchLength + additionalMatchBytes;

        return new Match(offset, matchLength);
    }

    #endregion Methods
}
