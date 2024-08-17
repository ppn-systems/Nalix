// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Shared.Memory.Internal;

namespace Nalix.Shared.LZ4.Encoders;

/// <summary>
/// Provides methods for compressing data using the LZ4 greedy compression algorithm.
/// This encoder is designed to achieve high performance and efficient compression.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
public static unsafe class LZ4BlockEncoder
{
    /// <summary>
    /// Calculates the maximum compressed length for a given input size.
    /// This is an estimate based on the input size and compression algorithm overheads.
    /// </summary>
    /// <param name="input">The size of the data to be compressed.</param>
    /// <returns>The estimated maximum length after compression, including overhead.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Int32 GetMaxLength(System.Int32 input) => input + (input / 255) + 16 + LZ4BlockHeader.Size;

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
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Int32 EncodeBlock(
        System.ReadOnlySpan<System.Byte> input,
        System.Span<System.Byte> output,
        System.Int32* hashTable)
    {
        if (input.IsEmpty || output.IsEmpty)
        {
            return -1; // Token empty input/output
        }

        // Pin the input and output spans to fixed memory addresses
        fixed (System.Byte* inputBase = &System.Runtime.InteropServices.MemoryMarshal.GetReference(input))
        {
            fixed (System.Byte* outputBase = &System.Runtime.InteropServices.MemoryMarshal.GetReference(output))
            {
                return EncodeInternal(inputBase, input.Length, outputBase, output.Length, hashTable);
            }
        }
    }

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
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    private static System.Int32 EncodeInternal(
        System.Byte* inputBase,
        System.Int32 inputLength,
        System.Byte* outputBase,
        System.Int32 outputLength,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Int32* hashTable)
    {
        System.Byte* inputPtr = inputBase;
        System.Byte* inputEnd = inputBase + inputLength;

        System.Byte* outputPtr = outputBase;
        System.Byte* outputEnd = outputBase + outputLength;

        // For very small blocks, greedy match is overkill; write a single literal run.
        const System.Int32 tinyLimit = LZ4CompressionConstants.LastLiteralSize + LZ4CompressionConstants.MinMatchLength;
        if (inputLength <= tinyLimit)
        {
            System.Int32 literalLen = inputLength;

            // exact varint length for literals
            System.Int32 litVarBytes = (literalLen > LZ4CompressionConstants.TokenLiteralMask)
                ? ((literalLen - LZ4CompressionConstants.TokenLiteralMask) / 255) + 1
                : 0;

            // need: 1 token + litVarBytes + literalLen
            System.Int32 need = 1 + litVarBytes + literalLen;
            if ((outputEnd - outputPtr) < need)
            {
                return -1;
            }

            System.Byte literalNibble = (System.Byte)System.Math.Min(literalLen, LZ4CompressionConstants.TokenLiteralMask);
            *outputPtr++ = (System.Byte)(literalNibble << 4);

            if (litVarBytes != 0)
            {
                outputPtr += SpanOps.WriteVarInt(outputPtr, literalLen - LZ4CompressionConstants.TokenLiteralMask);
            }

            LiteralWriter.Write(ref outputPtr, inputBase, literalLen);
            return (System.Int32)(outputPtr - outputBase);
        }
        // -----------------------------------------------

        System.Byte* literalStartPtr = inputBase;

        // Leave room for last literal & min match
        System.Byte* matchFindInputLimit = inputEnd - LZ4CompressionConstants.LastLiteralSize - LZ4CompressionConstants.MinMatchLength;

        // Validate output capacity baseline
        if (outputLength < 1)
        {
            return -1;
        }

        while (inputPtr < matchFindInputLimit)
        {
            System.Int32 currentInputOffset = (System.Int32)(inputPtr - inputBase);
            System.Byte* windowStartPtr = inputBase + System.Math.Max(0, currentInputOffset - LZ4CompressionConstants.MaxOffset);

            var match = MatchFinder.FindLongestMatch(
                hashTable,
                inputBase,
                inputPtr,
                inputEnd - LZ4CompressionConstants.LastLiteralSize,
                windowStartPtr,
                currentInputOffset);

            if (!match.Found)
            {
                inputPtr++;
                continue;
            }

            System.Int32 literalLength = (System.Int32)(inputPtr - literalStartPtr);
            System.Int32 matchLength = match.Length;
            System.Int32 offset = match.Offset;

            if (!WriteSequence(ref outputPtr, outputEnd, literalStartPtr, literalLength, matchLength, offset))
            {
                return -1;
            }

            inputPtr += matchLength;
            literalStartPtr = inputPtr;
        }

        if (!WriteFinalLiterals(ref outputPtr, outputEnd, literalStartPtr, (System.Int32)(inputEnd - literalStartPtr)))
        {
            return -1;
        }

        return (System.Int32)(outputPtr - outputBase);
    }

    /// <summary>
    /// Writes a sequence of literals and a match to the output buffer.
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    private static System.Boolean WriteSequence(
        ref System.Byte* outputPtr,
        System.Byte* outputEnd,
        System.Byte* literalStartPtr,
        System.Int32 literalLength,
        System.Int32 matchLength,
        System.Int32 offset)
    {
        const System.Int32 tokenLen = 1;
        const System.Int32 matchThreshold = LZ4CompressionConstants.MinMatchLength + LZ4CompressionConstants.TokenMatchMask + 1;

        System.Int32 litVarBytes;
        System.Int32 matchVarBytes;

        // literal varint bytes 
        if (literalLength > LZ4CompressionConstants.TokenLiteralMask)
        {
            litVarBytes = ((literalLength - LZ4CompressionConstants.TokenLiteralMask) / 255) + 1;
        }
        else
        {
            litVarBytes = 0;
        }

        // match varint bytes (NOTE: threshold includes MinMatchLength)
        if (matchLength >= matchThreshold)
        {
            matchVarBytes = ((matchLength - (LZ4CompressionConstants.MinMatchLength + LZ4CompressionConstants.TokenMatchMask)) / 255) + 1;
        }
        else
        {
            matchVarBytes = 0;
        }

        // exact required space
        System.Int32 required = tokenLen + litVarBytes + literalLength + sizeof(System.UInt16) + matchVarBytes;

        if (outputPtr + required > outputEnd)
        {
            return false;
        }

        // token
        System.Byte litTok = (System.Byte)System.Math.Min(literalLength, LZ4CompressionConstants.TokenLiteralMask);
        System.Byte matchTok = (System.Byte)System.Math.Min(matchLength - LZ4CompressionConstants.MinMatchLength, LZ4CompressionConstants.TokenMatchMask);

        *outputPtr++ = (System.Byte)((litTok << 4) | matchTok);

        // literal length varint
        if (litVarBytes != 0)
        {
            outputPtr += SpanOps.WriteVarInt(outputPtr, literalLength - LZ4CompressionConstants.TokenLiteralMask);
        }

        // literals
        LiteralWriter.Write(ref outputPtr, literalStartPtr, literalLength);

        // offset
        MemOps.WriteUnaligned<System.UInt16>(outputPtr, (System.UInt16)offset);
        outputPtr += sizeof(System.UInt16);

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
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    private static System.Boolean WriteFinalLiterals(
        ref System.Byte* outputPtr,
        System.Byte* outputEnd,
        System.Byte* literalStartPtr,
        System.Int32 literalLength)
    {
        const System.Int32 tokenLen = 1;

        if (literalLength == 0)
        {
            return true;
        }

        System.Int32 litVarBytes;
        if (literalLength > LZ4CompressionConstants.TokenLiteralMask)
        {
            litVarBytes = ((literalLength - LZ4CompressionConstants.TokenLiteralMask) / 255) + 1;
        }
        else
        {
            litVarBytes = 0;
        }

        System.Int32 required = tokenLen + litVarBytes + literalLength;
        if (outputPtr + required > outputEnd)
        {
            return false;
        }

        System.Byte litTok = (System.Byte)System.Math.Min(literalLength, LZ4CompressionConstants.TokenLiteralMask);
        *outputPtr++ = (System.Byte)(litTok << 4);

        if (litVarBytes != 0)
        {
            outputPtr += SpanOps.WriteVarInt(outputPtr, literalLength - LZ4CompressionConstants.TokenLiteralMask);
        }

        LiteralWriter.Write(ref outputPtr, literalStartPtr, literalLength);
        return true;
    }
}
