// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Shared.LZ4.Encoders;
using Nalix.Shared.Memory.Internal;

namespace Nalix.Shared.LZ4.Engine;

/// <summary>
/// Provides functionality to compress data using the LZ4 algorithm, optimized for zero-allocation and high efficiency.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
internal static class LZ4Encoder
{
    #region APIs

    /// <summary>
    /// Compresses the provided input data into the specified output buffer.
    /// </summary>
    /// <param name="input">The input data to compress as a <see cref="System.ReadOnlySpan{T}"/>.</param>
    /// <param name="output">The buffer where the compressed data will be written. Must have enough capacity.</param>
    /// <returns>
    /// The total number of bytes written to the output buffer (including the header),
    /// or -1 if the output buffer is too small or compression fails.
    /// </returns>
    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static unsafe System.Int32 Encode(System.ReadOnlySpan<System.Byte> input, System.Span<System.Byte> output)
    {
        // Token empty input
        if (input.IsEmpty)
        {
            return WriteEmptyHeader(output);
        }

        if (output.Length < LZ4BlockHeader.Size)
        {
            return -1;
        }

        if (output.Length < LZ4BlockEncoder.GetMaxLength(input.Length))
        {
            System.Diagnostics.Debug.WriteLine(
                $"Warning: Output buffer may be too small. Required: {LZ4BlockEncoder.GetMaxLength(input.Length)}, Available: {output.Length}");
        }

        // Rent hash table once per call, reuse across the whole encode.
        // Table size equals MatchFinder.HashTableSize.
        System.Int32[] table = System.Buffers.ArrayPool<System.Int32>.Shared.Rent(MatchFinder.HashTableSize);
        try
        {
            // Clear using vectorized Span.Clear() (fast in CoreCLR)
            System.Runtime.InteropServices.MemoryMarshal.CreateSpan(ref table[0], MatchFinder.HashTableSize)
                                                        .Clear();

            // Pin to obtain a stable pointer for the duration of EncodeBlock
            fixed (System.Int32* hashTable = table)
            {
#if DEBUG
                System.Diagnostics.Debug.Assert(hashTable is not null, "Hash table pinning failed");
#endif
                if (hashTable == null)
                {
                    throw new System.InvalidOperationException("Failed to pin hash table");
                }

                System.Span<System.Byte> compressedDataOutput = output[LZ4BlockHeader.Size..];
                System.Int32 compressedDataLength =
                    Encoders.LZ4BlockEncoder.EncodeBlock(input, compressedDataOutput, hashTable);

                if (compressedDataLength < 0)
                {
                    return -1;
                }

                System.Int32 totalCompressedLength = LZ4BlockHeader.Size + compressedDataLength;
#if DEBUG
                System.Boolean isValid = totalCompressedLength <= output.Length;
                System.Diagnostics.Debug.Assert(isValid);
#endif

                if (totalCompressedLength > output.Length)
                {
                    throw new System.InvalidOperationException(
                        $"Compressed data ({totalCompressedLength} bytes) exceeds output buffer ({output.Length} bytes)");
                }

                WriteHeader(output, input.Length, totalCompressedLength);
                return totalCompressedLength;
            }
        }
        finally
        {
            System.Buffers.ArrayPool<System.Int32>.Shared.Return(table, clearArray: false);
        }
    }

    #endregion APIs

    #region Private Methods

    /// <summary>
    /// Writes a header for an empty input to the output buffer.
    /// </summary>
    /// <param name="output">The output buffer to write the header into.</param>
    /// <returns>The size of the header, or -1 if the buffer is too small.</returns>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Int32 WriteEmptyHeader(System.Span<System.Byte> output)
    {
        if (output.Length < LZ4BlockHeader.Size)
        {
            return -1;
        }

        LZ4BlockHeader header = new(0, LZ4BlockHeader.Size);
        MemOps.WriteUnaligned(output, header);
        return LZ4BlockHeader.Size;
    }

    /// <summary>
    /// Writes the header to the output buffer.
    /// </summary>
    /// <param name="output">The output buffer to write the header into.</param>
    /// <param name="originalLength">The original length of the input data.</param>
    /// <param name="compressedLength">The total compressed length, including the header.</param>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void WriteHeader(System.Span<System.Byte> output, System.Int32 originalLength, System.Int32 compressedLength)
    {
        LZ4BlockHeader header = new(originalLength, compressedLength);
        MemOps.WriteUnaligned(output, header);
    }

    #endregion Private Methods
}
