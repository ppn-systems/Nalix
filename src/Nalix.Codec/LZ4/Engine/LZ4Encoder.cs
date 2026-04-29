// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Nalix.Codec.Internal;

namespace Nalix.Codec.LZ4.Engine;

/// <summary>
/// Provides functionality to compress data using the LZ4 algorithm, optimized for zero-allocation and high efficiency.
/// </summary>
[DebuggerNonUserCode]
[EditorBrowsable(EditorBrowsableState.Never)]
internal static class LZ4Encoder
{
    #region APIs

    /// <summary>
    /// Compresses the provided input data into the specified output buffer.
    /// </summary>
    /// <param name="input">The input data to compress as a <see cref="ReadOnlySpan{T}"/>.</param>
    /// <param name="output">The buffer where the compressed data will be written. Must have enough capacity.</param>
    /// <returns>The total number of bytes written to the output buffer (including the header).</returns>
    [StackTraceHidden]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    public static unsafe int Encode(ReadOnlySpan<byte> input, Span<byte> output)
    {
        if (input.IsEmpty)
        {
            return WriteEmptyHeader(output);
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(output.Length, LZ4BlockHeader.Size, nameof(output));

#if DEBUG
        if (output.Length < LZ4BlockEncoder.GetMaxLength(input.Length))
        {
            Debug.WriteLine(
                $"Warn: Output buffer may be too small. Required: {LZ4BlockEncoder.GetMaxLength(input.Length)}, Available: {output.Length}");
        }
#endif

        int hashBits = GetHashBits(input.Length);
        int[] table = LZ4HashTablePool.Rent(hashBits);

        try
        {
            fixed (int* hashTable = table)
            {
                // Safety: We slice the output span to exclude the header area.
                // This ensures LZ4BlockEncoder.EncodeBlock can only write into the remaining space.
                Span<byte> compressedDataOutput = output.Slice(LZ4BlockHeader.Size);
                
                int compressedDataLength =
                    LZ4BlockEncoder.EncodeBlock(input, compressedDataOutput, hashTable, hashBits);

                // If EncodeBlock returns -1, it means the output buffer was too small to hold 
                // the compressed data. We bail out before writing the header to avoid data corruption.
                if (compressedDataLength < 0)
                {
                    throw CodecErrors.LZ4EncoderOutputBufferTooSmall;
                }

                int totalCompressedLength = LZ4BlockHeader.Size + compressedDataLength;

                // Write the header only after successful compression of the data block.
                WriteHeader(output, input.Length, totalCompressedLength);
                return totalCompressedLength;
            }
        }
        finally
        {
            LZ4HashTablePool.Return(table);
        }
    }

    #endregion APIs

    #region Private Methods

    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WriteEmptyHeader(Span<byte> output)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(output.Length, LZ4BlockHeader.Size, nameof(output));

        LZ4BlockHeader header = new(0, LZ4BlockHeader.Size);
        MemOps.WriteUnaligned(output, header);
        return LZ4BlockHeader.Size;
    }

    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteHeader(Span<byte> output, int originalLength, int compressedLength)
    {
        LZ4BlockHeader header = new(originalLength, compressedLength);
        MemOps.WriteUnaligned(output, header);
    }

    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetHashBits(int inputLength)
    {
        // Default to max dictionary size (65536 entries, 256KB) for 64KB+ inputs
        if (inputLength >= 65536)
        {
            return 16;
        }

        // Calculate bits based on input length, using a conservative jump threshold (1.5x)
        // to avoid wasting ArrayPool resources for inputs just over a power of 2.
        int bits = System.Numerics.BitOperations.Log2((uint)inputLength);

        if (bits > 8 && inputLength < (1 << bits) + (1 << (bits - 1)))
        {
            bits--;
        }

        // Minimum 8 bits (256 entries) to ensure decent match finding for small blocks.
        // Clamped at 16 bits maximum (handled by the if check above).
        return Math.Max(8, bits);
    }

    #endregion Private Methods
}
