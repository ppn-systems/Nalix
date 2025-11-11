// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Shared.LZ4.Encoders;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.LZ4.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.LZ4.Benchmarks")]
#endif

namespace Nalix.Shared.Memory.Internal;

/// <summary>
/// Provides functionality for finding matches in input data using a hash table.
/// Optimized for high performance in LZ4 compression.
/// </summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
internal static unsafe class MatchFinder
{
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
        // Ensure there are enough bytes to find a match
        if ((System.UIntPtr)(inputLimit - currentInputPtr) < LZ4CompressionConstants.MinMatchLength)
        {
            return default; // No match possible
        }

        // Read the current 4-byte sequence and compute its hash
        System.UInt32 currentSequence = MemOps.ReadUnaligned<System.UInt32>(currentInputPtr);
        System.UInt32 hash = currentSequence * 2654435761u >> HashShift;

        // Retrieve the candidate match offset and update the hash table
        System.Int32 matchCandidateOffset = hashTable[hash];
        hashTable[hash] = currentInputOffset;

        if ((System.UInt32)matchCandidateOffset >= (System.UInt32)currentInputOffset)
        {
            return default;
        }

        // Calculate the candidate match pointer
        System.Byte* matchCandidatePtr = inputBase + matchCandidateOffset;

        if (matchCandidatePtr < searchStartPtr)
        {
            return default;
        }

        if (MemOps.ReadUnaligned<System.UInt32>(matchCandidatePtr) != currentSequence)
        {
            return default;
        }

        // Calculate the length of the match
        System.Int32 matchLength = LZ4CompressionConstants.MinMatchLength + MemOps.CountEqualBytes(
            matchCandidatePtr + LZ4CompressionConstants.MinMatchLength,
            currentInputPtr + LZ4CompressionConstants.MinMatchLength,
            (System.Int32)(inputLimit - (currentInputPtr + LZ4CompressionConstants.MinMatchLength)) // Ensure bounds
        );

        // Calculate the offset of the match
        System.Int32 offset = (System.Int32)(currentInputPtr - matchCandidatePtr);

        // Ensure the offset is within the valid range
        System.Diagnostics.Debug.Assert(offset is > 0 and <= LZ4CompressionConstants.MaxOffset);

        return new Match(offset, matchLength);
    }
}
