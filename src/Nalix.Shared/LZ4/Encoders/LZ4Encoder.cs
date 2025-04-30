using Nalix.Shared.LZ4.Internal;

namespace Nalix.Shared.LZ4.Encoders;

/// <summary>
/// Provides methods for compressing data using the LZ4 greedy compression algorithm.
/// This encoder is designed to achieve high performance and efficient compression.
/// </summary>
public static unsafe class LZ4Encoder
{
    /// <summary>
    /// Calculates the maximum compressed length for a given input size.
    /// This is an estimate based on the input size and compression algorithm overheads.
    /// </summary>
    /// <param name="input">The size of the data to be compressed.</param>
    /// <returns>The estimated maximum length after compression, including overhead.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static int GetMaxLength(int input) => input + (input / 250) + Header.Size;

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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static int EncodeBlock(
        System.ReadOnlySpan<byte> input,
        System.Span<byte> output,
        int* hashTable)
    {
        if (input.IsEmpty || output.IsEmpty) return -1; // Handle empty input/output

        // Pin the input and output spans to fixed memory addresses
        fixed (byte* inputBase = &System.Runtime.InteropServices.MemoryMarshal.GetReference(input))
        fixed (byte* outputBase = &System.Runtime.InteropServices.MemoryMarshal.GetReference(output))
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static int EncodeInternal(
        byte* inputBase,
        int inputLength,
        byte* outputBase,
        int outputLength,
        int* hashTable)
    {
        byte* inputPtr = inputBase;
        byte* inputEnd = inputBase + inputLength;

        byte* outputPtr = outputBase;
        byte* outputEnd = outputBase + outputLength;

        byte* literalStartPtr = inputBase; // Start of the current literal run

        // Leave room for the last literal run and match check
        byte* matchFindInputLimit = inputEnd - LZ4Constants.LastLiteralSize - LZ4Constants.MinMatchLength;

        // Validate output buffer size
        if (outputLength < Header.Size + 1) return -1;

        // Main compression loop
        while (inputPtr < matchFindInputLimit)
        {
            int currentInputOffset = (int)(inputPtr - inputBase);
            byte* windowStartPtr = inputBase + System.Math.Max(0, currentInputOffset - LZ4Constants.MaxOffset);

            // Find the longest match for the current input block
            MatchFinder.Match match = MatchFinder.FindLongestMatch(
                hashTable,
                inputBase,
                inputPtr,
                inputEnd - LZ4Constants.LastLiteralSize,
                windowStartPtr,
                currentInputOffset);

            if (!match.Found)
            {
                inputPtr++; // No match found, continue with literal run
                continue;
            }

            // Emit literal run and match token
            int literalLength = (int)(inputPtr - literalStartPtr);
            int matchLength = match.Length;
            int offset = match.Offset;

            // Write literals and match sequence
            if (!WriteSequence(ref outputPtr, outputEnd, literalStartPtr, literalLength, matchLength, offset))
            {
                return -1; // Output buffer too small
            }

            // Advance pointers
            inputPtr += matchLength;
            literalStartPtr = inputPtr; // Start new literal run
        }

        // Emit final literals
        if (!WriteFinalLiterals(ref outputPtr, outputEnd, literalStartPtr, (int)(inputEnd - literalStartPtr)))
        {
            return -1; // Output buffer too small
        }

        return (int)(outputPtr - outputBase);
    }

    /// <summary>
    /// Writes a sequence of literals and a match to the output buffer.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static bool WriteSequence(
        ref byte* outputPtr,
        byte* outputEnd,
        byte* literalStartPtr,
        int literalLength,
        int matchLength,
        int offset)
    {
        // Calculate required space for this sequence
        int tokenLength = 1;
        int literalHeaderLength = (literalLength >= LZ4Constants.TokenLiteralMask)
            ? 1 + (literalLength - LZ4Constants.TokenLiteralMask) / 255 : 0;

        int matchHeaderLength = (matchLength >= LZ4Constants.TokenMatchMask)
            ? 1 + (matchLength - LZ4Constants.TokenMatchMask) / 255 : 0;

        int requiredSpace = tokenLength + literalHeaderLength + literalLength + sizeof(ushort) + matchHeaderLength;

        if (outputPtr + requiredSpace > outputEnd) return false;

        // Write the token
        byte literalToken = (byte)System.Math.Min(literalLength, LZ4Constants.TokenLiteralMask);
        byte matchToken = (byte)System.Math.Min(matchLength - LZ4Constants.MinMatchLength, LZ4Constants.TokenMatchMask);
        byte token = (byte)((literalToken << 4) | matchToken);
        *outputPtr++ = token;

        // Write literal length
        if (literalLength >= LZ4Constants.TokenLiteralMask)
        {
            outputPtr += SpanOps.WriteVarInt(outputPtr, literalLength - LZ4Constants.TokenLiteralMask);
        }

        // Write literals
        LiteralWriter.Write(ref outputPtr, literalStartPtr, literalLength);

        // Write offset
        MemOps.WriteUnaligned<ushort>(outputPtr, (ushort)offset);
        outputPtr += sizeof(ushort);

        // Write match length
        if (matchLength >= LZ4Constants.MinMatchLength + LZ4Constants.TokenMatchMask)
        {
            outputPtr += SpanOps.WriteVarInt(outputPtr, matchLength - LZ4Constants.MinMatchLength - LZ4Constants.TokenMatchMask);
        }

        return true;
    }

    /// <summary>
    /// Writes the final literals to the output buffer.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static bool WriteFinalLiterals(
        ref byte* outputPtr,
        byte* outputEnd,
        byte* literalStartPtr,
        int literalLength)
    {
        if (literalLength == 0) return true;

        int tokenLength = 1;
        int literalHeaderLength = (literalLength >= LZ4Constants.TokenLiteralMask)
            ? 1 + (literalLength - LZ4Constants.TokenLiteralMask) / 255
            : 0;

        int requiredSpace = tokenLength + literalHeaderLength + literalLength;

        if (outputPtr + requiredSpace > outputEnd) return false;

        byte literalToken = (byte)System.Math.Min(literalLength, LZ4Constants.TokenLiteralMask);
        byte token = (byte)(literalToken << 4); // Match part is 0
        *outputPtr++ = token;

        if (literalLength >= LZ4Constants.TokenLiteralMask)
        {
            outputPtr += SpanOps.WriteVarInt(outputPtr, literalLength - LZ4Constants.TokenLiteralMask);
        }

        LiteralWriter.Write(ref outputPtr, literalStartPtr, literalLength);
        return true;
    }
}
