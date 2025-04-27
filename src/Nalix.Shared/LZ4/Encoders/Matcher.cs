using Nalix.Shared.LZ4.Internal;
using System.Runtime.CompilerServices;

namespace Nalix.Shared.LZ4.Encoders;

/// <summary>
/// Match finder using a hash table.
/// </summary>
internal static unsafe class Matcher
{
    private const int HashTableBits = 16; // 64k entries
    public const int HashTableSize = 1 << HashTableBits;
    private const int HashShift = (32 - HashTableBits);

    // Consider making this configurable or using ArrayPool if stackalloc proves too large
    public const int MaxStackallocHashTableSize = HashTableSize * sizeof(int);

    /// <summary>
    /// Represents a found match.
    /// </summary>
    internal readonly struct Match(int offset, int length)
    {
        public readonly int Offset = offset;
        public readonly int Length = length;

        public bool Found => Length >= LZ4Constants.MinMatchLength;
    }

    /// <summary>
    /// Calculates a hash for 4 bytes.
    /// </summary>
    // Simple Hash (LZ4 uses a multiplication method)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint CalculateHash(uint sequence)
        => (sequence * 2654435761u) >> HashShift;

    /// <summary>
    /// Finds the longest match within the sliding window.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Match FindLongestMatch(
        int* hashTable,          // Hash table (maps hash to input position offset)
        byte* inputBase,         // Start of the entire input buffer
        byte* currentInputPtr,   // Current position in input we are trying to match
        byte* inputLimit,        // Limit for matching (usually currentPtr + MaxMatchLength or end of block)
        byte* searchStartPtr,    // The earliest position in the input to search (start of window)
        int currentInputOffset)  // Offset of currentInputPtr from inputBase
    {
        if (currentInputPtr + LZ4Constants.MinMatchLength > inputLimit) // Need at least 4 bytes to match
            return default; // No match possible

        uint currentSequence = MemOps.ReadUnaligned<uint>(currentInputPtr);
        uint hash = CalculateHash(currentSequence);

        int matchCandidateOffset = hashTable[hash]; // Get offset relative to inputBase
        hashTable[hash] = currentInputOffset;       // Update hash table with current position's offset

        byte* matchCandidatePtr = inputBase + matchCandidateOffset;

        // Validate candidate
        if (matchCandidateOffset <= 0 || // Hash slot empty/invalid
            matchCandidatePtr < searchStartPtr || // Outside window
            matchCandidatePtr >= currentInputPtr || // Cannot match self
            MemOps.ReadUnaligned<uint>(matchCandidatePtr) != currentSequence) // Quick check fails
        {
            return default; // No match found
        }

        // Calculate match length
        int matchLength = LZ4Constants.MinMatchLength + MemOps.CountEqualBytes(
            matchCandidatePtr + LZ4Constants.MinMatchLength,
            currentInputPtr + LZ4Constants.MinMatchLength,
            (int)(inputLimit - (currentInputPtr + LZ4Constants.MinMatchLength)) // Max length check
        );

        int offset = (int)(currentInputPtr - matchCandidatePtr);
        System.Diagnostics.Debug.Assert(offset > 0 && offset <= LZ4Constants.MaxOffset);

        return new Match(offset, matchLength);
    }
}
