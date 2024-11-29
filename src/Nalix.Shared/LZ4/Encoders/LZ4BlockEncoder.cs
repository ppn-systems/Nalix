using Nalix.Shared.LZ4.Internal;
using Nalix.Shared.Memory.Unsafe;

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
    public static System.Int32 GetMaxLength(System.Int32 input)
        => input + (input / 255) + 16 + LZ4BlockHeader.Size;

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
        fixed (System.Byte* outputBase = &System.Runtime.InteropServices.MemoryMarshal.GetReference(output))
        {
            return EncodeInternal(inputBase, input.Length, outputBase, output.Length, hashTable);
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

        System.Byte* literalStartPtr = inputBase; // Start of the current literal run

        // Leave room for the last literal run and match check
        System.Byte* matchFindInputLimit = inputEnd - LZ4CompressionConstants.LastLiteralSize - LZ4CompressionConstants.MinMatchLength;

        // Validate output buffer size
        if (outputLength < LZ4BlockHeader.Size + 1)
        {
            return -1;
        }

        // Main compression loop
        while (inputPtr < matchFindInputLimit)
        {
            System.Int32 currentInputOffset = (System.Int32)(inputPtr - inputBase);
            System.Byte* windowStartPtr = inputBase + System.Math.Max(0, currentInputOffset - LZ4CompressionConstants.MaxOffset);

            // Find the longest match for the current input block
            MatchFinder.Match match = MatchFinder.FindLongestMatch(
                hashTable,
                inputBase,
                inputPtr,
                inputEnd - LZ4CompressionConstants.LastLiteralSize,
                windowStartPtr,
                currentInputOffset);

            if (!match.Found)
            {
                inputPtr++; // No match found, continue with literal run
                continue;
            }

            // Emit literal run and match token
            System.Int32 literalLength = (System.Int32)(inputPtr - literalStartPtr);
            System.Int32 matchLength = match.Length;
            System.Int32 offset = match.Offset;

            // WriteInt16 literals and match sequence
            if (!WriteSequence(ref outputPtr, outputEnd, literalStartPtr, literalLength, matchLength, offset))
            {
                return -1; // Output buffer too small
            }

            // Advance pointers
            inputPtr += matchLength;
            literalStartPtr = inputPtr; // Start new literal run
        }

        // Emit final literals
        if (!WriteFinalLiterals(
            ref outputPtr, outputEnd,
            literalStartPtr, (System.Int32)(inputEnd - literalStartPtr)))
        {
            return -1; // Output buffer too small
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
        // Calculate required space for this sequence
        System.Int32 tokenLength = 1;
        System.Int32 literalHeaderLength =
            (literalLength >= LZ4CompressionConstants.TokenLiteralMask)
            ? 1 + ((literalLength - LZ4CompressionConstants.TokenLiteralMask) / 255) : 0;

        System.Int32 matchHeaderLength =
            (matchLength >= LZ4CompressionConstants.TokenMatchMask)
            ? 1 + ((matchLength - LZ4CompressionConstants.TokenMatchMask) / 255) : 0;

        System.Int32 requiredSpace =
            tokenLength +
            literalHeaderLength +
            literalLength +
            sizeof(System.UInt16) +
            matchHeaderLength;

        if (outputPtr + requiredSpace > outputEnd)
        {
            return false;
        }

        // WriteInt16 the token
        System.Byte matchToken = (System.Byte)
            System.Math.Min(matchLength - LZ4CompressionConstants.MinMatchLength, LZ4CompressionConstants.TokenMatchMask);
        System.Byte literalToken = (System.Byte)System.Math.Min(literalLength, LZ4CompressionConstants.TokenLiteralMask);

        System.Byte token = (System.Byte)((literalToken << 4) | matchToken);
        *outputPtr++ = token;

        // WriteInt16 literal length
        if (literalLength >= LZ4CompressionConstants.TokenLiteralMask)
        {
            outputPtr += SpanOps.WriteVarInt(outputPtr, literalLength - LZ4CompressionConstants.TokenLiteralMask);
        }

        // WriteInt16 literals
        LiteralWriter.Write(ref outputPtr, literalStartPtr, literalLength);

        // WriteInt16 offset
        MemOps.WriteUnaligned<System.UInt16>(outputPtr, (System.UInt16)offset);
        outputPtr += sizeof(System.UInt16);

        // WriteInt16 match length
        if (matchLength >= LZ4CompressionConstants.MinMatchLength + LZ4CompressionConstants.TokenMatchMask)
        {
            outputPtr += SpanOps.WriteVarInt(
                outputPtr,
                matchLength - LZ4CompressionConstants.MinMatchLength - LZ4CompressionConstants.TokenMatchMask);
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
        if (literalLength == 0)
        {
            return true;
        }

        System.Int32 tokenLength = 1;
        System.Int32 literalHeaderLength =
            (literalLength >= LZ4CompressionConstants.TokenLiteralMask)
            ? 1 + ((literalLength - LZ4CompressionConstants.TokenLiteralMask) / 255) : 0;

        System.Int32 requiredSpace = tokenLength + literalHeaderLength + literalLength;

        if (outputPtr + requiredSpace > outputEnd)
        {
            return false;
        }

        System.Byte literalToken = (System.Byte)System.Math.Min(literalLength, LZ4CompressionConstants.TokenLiteralMask);
        System.Byte token = (System.Byte)(literalToken << 4); // Match part is 0
        *outputPtr++ = token;

        if (literalLength >= LZ4CompressionConstants.TokenLiteralMask)
        {
            outputPtr += SpanOps.WriteVarInt(outputPtr, literalLength - LZ4CompressionConstants.TokenLiteralMask);
        }

        LiteralWriter.Write(ref outputPtr, literalStartPtr, literalLength);
        return true;
    }
}
