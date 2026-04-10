// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Nalix.Framework.LZ4.Encoders;
using Nalix.Framework.Memory.Internal;

namespace Nalix.Framework.LZ4.Engine;

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
#if DEBUG
                Debug.Assert(hashTable is not null, "Hash table pinning failed");
#endif

                Span<byte> compressedDataOutput = output[LZ4BlockHeader.Size..];
                int compressedDataLength =
                    LZ4BlockEncoder.EncodeBlock(input, compressedDataOutput, hashTable, hashBits);

                if (compressedDataLength < 0)
                {
                    throw new InvalidOperationException(
                        $"LZ4 compression failed because the destination buffer is too small. Input length: {input.Length}, Output length: {output.Length}.");
                }

                int totalCompressedLength = LZ4BlockHeader.Size + compressedDataLength;

#if DEBUG
                Debug.Assert(
                    totalCompressedLength <= output.Length,
                    $"Compressed data ({totalCompressedLength} bytes) exceeds output buffer ({output.Length} bytes)");
#endif

                if (totalCompressedLength > output.Length)
                {
                    throw new InvalidOperationException(
                        $"Compressed data ({totalCompressedLength} bytes) exceeds output buffer ({output.Length} bytes)");
                }

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

        int bits = System.Numerics.BitOperations.Log2((uint)inputLength);
        
        // Clamp to avoid tiny hash tables handling too much traffic or huge tables for empty data
        if (bits < 8) bits = 8;
        if (bits > 16) bits = 16;

        return bits;
    }

    #endregion Private Methods
}
