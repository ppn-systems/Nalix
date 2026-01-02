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
    /// <returns>
    /// The total number of bytes written to the output buffer (including the header),
    /// or -1 if the output buffer is too small or compression fails.
    /// </returns>
    /// <exception cref="InvalidOperationException"></exception>
    [StackTraceHidden]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining |
        MethodImplOptions.AggressiveOptimization)]
    public static unsafe int Encode(ReadOnlySpan<byte> input, Span<byte> output)
    {
        if (input.IsEmpty)
        {
            return WriteEmptyHeader(output);
        }

        if (output.Length < LZ4BlockHeader.Size)
        {
            return -1;
        }

#if DEBUG
        if (output.Length < LZ4BlockEncoder.GetMaxLength(input.Length))
        {
            Debug.WriteLine(
                $"Warn: Output buffer may be too small. Required: {LZ4BlockEncoder.GetMaxLength(input.Length)}, Available: {output.Length}");
        }
#endif

        // LZ4HashTablePool dùng [ThreadStatic] — không có lock/CAS overhead như ArrayPool.Shared,
        // và tự Clear() bên trong Rent() nên không cần clear thủ công sau khi lấy ra.
        int[] table = LZ4HashTablePool.Rent();

        try
        {
            fixed (int* hashTable = table)
            {
#if DEBUG
                Debug.Assert(hashTable is not null, "Hash table pinning failed");
#endif

                Span<byte> compressedDataOutput = output[LZ4BlockHeader.Size..];
                int compressedDataLength =
                    LZ4BlockEncoder.EncodeBlock(input, compressedDataOutput, hashTable);

                if (compressedDataLength < 0)
                {
                    return -1;
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
            // No-op cho thread-static pool, nhưng giữ để API consistent
            // và dễ swap sang implementation khác sau này
            LZ4HashTablePool.Return(table);
        }
    }

    #endregion APIs

    #region Private Methods

    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WriteEmptyHeader(Span<byte> output)
    {
        if (output.Length < LZ4BlockHeader.Size)
        {
            return -1;
        }

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

    #endregion Private Methods
}
