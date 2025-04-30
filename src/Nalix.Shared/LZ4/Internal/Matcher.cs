using Nalix.Shared.LZ4.Encoders;

namespace Nalix.Shared.LZ4.Internal;

/// <summary>
/// Provides functionality for finding matches in input data using a hash table.
/// Optimized for high performance in LZ4 compression.
/// </summary>
internal static unsafe class Matcher
{
    private const int HashTableBits = 16; // 64k entries
    private const int HashShift = 32 - HashTableBits;

    /// <summary>
    /// Size of the hash table used for storing offsets of previously seen sequences.
    /// </summary>
    public const int HashTableSize = 1 << HashTableBits;

    /// <summary>
    /// Consider using a pool or limiting stackalloc in extreme cases
    /// </summary>
    public const int MaxStackallocHashTableSize = HashTableSize * sizeof(int);

    /// <summary>
    /// Represents a found match with an offset and length.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="Match"/> struct.
    /// </remarks>
    internal readonly struct Match(int offset, int length)
    {
        public readonly int Offset = offset; // Match offset relative to current position
        public readonly int Length = length; // Length of the match

        /// <summary>
        /// Determines if the match is valid.
        /// A match is considered valid if its length meets the minimum match length.
        /// </summary>
        public bool Found => Length >= LZ4Constants.MinMatchLength;
    }

    /// <summary>
    /// Finds the longest match for the current input position within the sliding window.
    /// </summary>
    /// <param name="hashTable">Hash table mapping hash values to input offsets.</param>
    /// <param name="inputBase">Pointer to the start of the input buffer.</param>
    /// <param name="currentInputPtr">Pointer to the current position in the input buffer.</param>
    /// <param name="inputLimit">Pointer to the end of the input buffer or match limit.</param>
    /// <param name="searchStartPtr">Pointer to the start of the sliding window for searching.</param>
    /// <param name="currentInputOffset">Offset of the current input pointer relative to the input base.</param>
    /// <returns>A <see cref="Match"/> struct representing the longest match found, or a default value if no match is found.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static Match FindLongestMatch(
        int* hashTable,
        byte* inputBase,
        byte* currentInputPtr,
        byte* inputLimit,
        byte* searchStartPtr,
        int currentInputOffset)
    {
        // Ensure there are enough bytes to find a match
        if (currentInputPtr + LZ4Constants.MinMatchLength > inputLimit)
            return default; // No match possible

        // Read the current 4-byte sequence and compute its hash
        uint currentSequence = MemOps.ReadUnaligned<uint>(currentInputPtr);
        uint hash = CalculateHash(currentSequence);

        // Retrieve the candidate match offset and update the hash table
        int matchCandidateOffset = hashTable[hash];
        hashTable[hash] = currentInputOffset;

        // Calculate the candidate match pointer
        byte* matchCandidatePtr = inputBase + matchCandidateOffset;

        // Validate the candidate match
        if (matchCandidateOffset <= 0 || // Invalid offset
            matchCandidatePtr < searchStartPtr || // Outside of search window
            matchCandidatePtr >= currentInputPtr || // Cannot match itself
            MemOps.ReadUnaligned<uint>(matchCandidatePtr) != currentSequence) // Quick check fails
        {
            return default; // No valid match found
        }

        // Calculate the length of the match
        int matchLength = LZ4Constants.MinMatchLength + MemOps.CountEqualBytes(
            matchCandidatePtr + LZ4Constants.MinMatchLength,
            currentInputPtr + LZ4Constants.MinMatchLength,
            (int)(inputLimit - (currentInputPtr + LZ4Constants.MinMatchLength)) // Ensure bounds
        );

        // Calculate the offset of the match
        int offset = (int)(currentInputPtr - matchCandidatePtr);

        // Ensure the offset is within the valid range
        System.Diagnostics.Debug.Assert(offset > 0 && offset <= LZ4Constants.MaxOffset);

        return new Match(offset, matchLength);
    }

    /// <summary>
    /// Computes a hash for a 4-byte sequence using a fast multiplication-based method.
    /// </summary>
    /// <param name="sequence">The 4-byte sequence to hash.</param>
    /// <returns>The hash value.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static uint CalculateHash(uint sequence) => sequence * 2654435761u >> HashShift;
}
