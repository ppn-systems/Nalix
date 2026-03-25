// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Shared.Memory.Internal;

namespace Nalix.Shared.LZ4.Encoders;

/// <summary>
/// Provides methods for compressing data using the LZ4 greedy compression algorithm.
/// This encoder is designed to achieve high performance and efficient compression.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public static class LZ4BlockEncoder
{
    #region APIs

    /// <summary>
    /// Calculates the maximum compressed length for a given input size.
    /// This is an estimate based on the input size and compression algorithm overheads.
    /// </summary>
    /// <param name="input">The size of the data to be compressed.</param>
    /// <returns>The estimated maximum length after compression, including overhead.</returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static int GetMaxLength(int input) => input + (input / 255) + 16 + LZ4BlockHeader.Size;

    /// <inheritdoc />
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
    System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static int GetMinOutputBufferSize(int inputLength) => inputLength + (inputLength / 255) + 16;

    /// <summary>
    /// Compresses a block of input data into the output buffer using the LZ4 greedy algorithm.
    /// </summary>
    /// <param name="input">The input data to be compressed.</param>
    /// <param name="output">The output buffer to store the compressed data.</param>
    /// <param name="hashTable">
    /// A pointer to a hash table used for finding matches. This can be stack-allocated or pooled.
    /// </param>
    /// <returns>
    /// The length of the compressed data, or -1 if the output buffer is too small.
    /// </returns>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static unsafe int EncodeBlock(System.ReadOnlySpan<byte> input, System.Span<byte> output, int* hashTable)
    {
        if (input.IsEmpty || output.IsEmpty)
        {
            return -1; // Token empty input/output
        }

        // Pin the input and output spans to fixed memory addresses
        fixed (byte* inputBase = &System.Runtime.InteropServices.MemoryMarshal.GetReference(input))
        {
            fixed (byte* outputBase = &System.Runtime.InteropServices.MemoryMarshal.GetReference(output))
            {
                return EncodeInternal(inputBase, input.Length, outputBase, output.Length, hashTable);
            }
        }
    }

    #endregion APIs

    #region Private Methods

    /// <summary>
    /// Core encoding logic. Processes input data and writes compressed output.
    /// </summary>
    /// <param name="inputBase">Pointer to the start of the input data.</param>
    /// <param name="inputLength">Length of the input data.</param>
    /// <param name="outputBase">Pointer to the start of the output buffer.</param>
    /// <param name="outputLength">Length of the output buffer.</param>
    /// <param name="hashTable">Pointer to the hash table for finding matches.</param>
    /// <returns>
    /// The length of the compressed data, or -1 if the output buffer is too small.
    /// </returns>
    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    private static unsafe int EncodeInternal(
        byte* inputBase, int inputLength,
        byte* outputBase, int outputLength,
        [System.Diagnostics.CodeAnalysis.NotNull] int* hashTable)
    {
#if DEBUG
        System.Diagnostics.Debug.Assert(hashTable is not null, "Hash table cannot be null");
#endif

        if (inputBase == null)
        {
            throw new System.ArgumentNullException(nameof(inputBase));
        }

        if (outputBase == null)
        {
            throw new System.ArgumentNullException(nameof(outputBase));
        }

        if (hashTable == null)
        {
            throw new System.ArgumentNullException(nameof(hashTable));
        }

        if (inputLength <= 0)
        {
            return 0;
        }

        if (outputLength < GetMinOutputBufferSize(inputLength))
        {
            return -1;
        }

        byte* inputPtr = inputBase;
        byte* inputEnd = inputBase + inputLength;

        byte* outputPtr = outputBase;
        byte* outputEnd = outputBase + outputLength;

        // For very small blocks, greedy match is overkill; write a single literal run.
        const int tinyLimit = LZ4CompressionConstants.LastLiteralSize + LZ4CompressionConstants.MinMatchLength;
        if (inputLength <= tinyLimit)
        {
            int literalLen = inputLength;

            // exact varint length for literals
            int litVarBytes = (literalLen >= LZ4CompressionConstants.TokenLiteralMask)
                ? ((literalLen - LZ4CompressionConstants.TokenLiteralMask) / 255) + 1
                : 0;

            // need: 1 token + litVarBytes + literalLen
            int need = 1 + litVarBytes + literalLen;
            if ((outputEnd - outputPtr) < need)
            {
                return -1;
            }

            byte literalNibble = (byte)System.Math.Min(literalLen, LZ4CompressionConstants.TokenLiteralMask);
            *outputPtr++ = (byte)(literalNibble << 4);

            if (litVarBytes != 0)
            {
                outputPtr += SpanOps.WriteVarInt(outputPtr, literalLen - LZ4CompressionConstants.TokenLiteralMask);
            }

            LiteralWriter.Write(ref outputPtr, inputBase, literalLen);
            return (int)(outputPtr - outputBase);
        }
        // -----------------------------------------------

        byte* literalStartPtr = inputBase;

        // Leave room for last literal & min match
        byte* matchFindInputLimit = inputEnd - LZ4CompressionConstants.LastLiteralSize - LZ4CompressionConstants.MinMatchLength;

        // Validate output capacity baseline
        if (matchFindInputLimit < inputBase)
        {
            matchFindInputLimit = inputBase;
        }

        while (inputPtr < matchFindInputLimit)
        {
            int currentInputOffset = (int)(inputPtr - inputBase);
            byte* windowStartPtr = inputBase + System.Math.Max(0, currentInputOffset - LZ4CompressionConstants.MaxOffset);

            MatchFinder.Match match = MatchFinder.FindLongestMatch(
                hashTable, inputBase, inputPtr,
                inputEnd - LZ4CompressionConstants.LastLiteralSize,
                windowStartPtr, currentInputOffset);

            if (!match.Found)
            {
                inputPtr++;
                continue;
            }

            int literalLength = (int)(inputPtr - literalStartPtr);
            int matchLength = match.Length;
            int offset = match.Offset;

            if (!WriteSequence(ref outputPtr, outputEnd, literalStartPtr, literalLength, matchLength, offset))
            {
                return -1;
            }

            inputPtr += matchLength;
            literalStartPtr = inputPtr;
        }

        return !WriteFinalLiterals(ref outputPtr, outputEnd, literalStartPtr, (int)(inputEnd - literalStartPtr)) ? -1 : (int)(outputPtr - outputBase);
    }

    /// <summary>
    /// Writes a sequence of literals and a match to the output buffer.
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    private static unsafe bool WriteSequence(
        ref byte* outputPtr,
        byte* outputEnd, byte* literalStartPtr,
        int literalLength, int matchLength, int offset)
    {
        const int tokenLen = 1;
        const int matchThreshold = LZ4CompressionConstants.MinMatchLength
                                          + LZ4CompressionConstants.TokenMatchMask;

        // literal varint bytes
        int litVarBytes = (literalLength >= LZ4CompressionConstants.TokenLiteralMask)
            ? ((literalLength - LZ4CompressionConstants.TokenLiteralMask) / 255) + 1
            : 0;

        // match varint bytes
        int matchVarBytes = (matchLength >= matchThreshold)
            ? ((matchLength - (LZ4CompressionConstants.MinMatchLength + LZ4CompressionConstants.TokenMatchMask)) / 255) + 1
            : 0;

        // exact required space
        int required = tokenLen + litVarBytes + literalLength + sizeof(ushort) + matchVarBytes;

        if (outputPtr + required > outputEnd)
        {
            return false;
        }

        // token
        byte litTok = (byte)System.Math.Min(literalLength, LZ4CompressionConstants.TokenLiteralMask);
        byte matchTok = (byte)System.Math.Min(matchLength - LZ4CompressionConstants.MinMatchLength, LZ4CompressionConstants.TokenMatchMask);

        *outputPtr++ = (byte)((litTok << 4) | matchTok);

        // literal length varint
        if (litVarBytes != 0)
        {
            outputPtr += SpanOps.WriteVarInt(outputPtr, literalLength - LZ4CompressionConstants.TokenLiteralMask);
        }

        // literals
        LiteralWriter.Write(ref outputPtr, literalStartPtr, literalLength);

        // offset
        MemOps.WriteUnaligned(outputPtr, (ushort)offset);
        outputPtr += sizeof(ushort);

        // match length varint
        if (matchVarBytes != 0)
        {
            outputPtr += SpanOps.WriteVarInt(outputPtr, matchLength - (LZ4CompressionConstants.MinMatchLength + LZ4CompressionConstants.TokenMatchMask));
        }

        return true;
    }

    /// <summary>
    /// Writes the final literals to the output buffer.
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    private static unsafe bool WriteFinalLiterals(
        ref byte* outputPtr, byte* outputEnd, byte* literalStartPtr, int literalLength)
    {
        const int tokenLen = 1;

        if (literalLength == 0)
        {
            return true;
        }

        int litVarBytes = (literalLength >= LZ4CompressionConstants.TokenLiteralMask)
            ? ((literalLength - LZ4CompressionConstants.TokenLiteralMask) / 255) + 1
            : 0;

        int required = tokenLen + litVarBytes + literalLength;
        if (outputPtr + required > outputEnd)
        {
            return false;
        }

        byte litTok = (byte)System.Math.Min(literalLength, LZ4CompressionConstants.TokenLiteralMask);
        *outputPtr++ = (byte)(litTok << 4);

        if (litVarBytes != 0)
        {
            outputPtr += SpanOps.WriteVarInt(outputPtr, literalLength - LZ4CompressionConstants.TokenLiteralMask);
        }

        LiteralWriter.Write(ref outputPtr, literalStartPtr, literalLength);
        return true;
    }

    #endregion Private Methods
}
