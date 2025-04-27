using Nalix.Shared.LZ4.Internal;

namespace Nalix.Shared.LZ4.Encoders;

/// <summary>
/// Implements the main greedy compression path (LZ4 style).
/// </summary>
public static unsafe class LZ4Compressor
{
    /// <summary>
    /// Calculates the maximum compressed length for a given input size.
    /// This is an estimate based on the input size and compression algorithm overheads.
    /// </summary>
    /// <param name="input">The size of the data to be compressed.</param>
    /// <returns>The estimated maximum length after compression, including overhead.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static int GetMaxLength(int input) => input + (input / 250) + 8;

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static int EncodeBlock(
        System.ReadOnlySpan<byte> input,
        System.Span<byte> output,
        int* hashTable) // Pointer to stackalloc'd or pooled hash table
    {
        fixed (byte* inputBase = &System.Runtime.InteropServices.MemoryMarshal.GetReference(input))
        fixed (byte* outputBase = &System.Runtime.InteropServices.MemoryMarshal.GetReference(output))
        {
            byte* inputPtr = inputBase;
            byte* inputEnd = inputBase + input.Length;
            // Leave room for the last literal run + match check
            byte* matchFindInputLimit = inputEnd - LZ4Constants.LastLiteralSize - LZ4Constants.MinMatchLength;

            byte* outputPtr = outputBase;
            byte* outputEnd = outputBase + output.Length;

            byte* literalStartPtr = inputBase; // Start of the current literal run

            // Ensure output buffer has minimum space for header + potential minimal data
            if (output.Length < Header.Size + 1) return -1;

            // Main loop for matching and compression
            while (inputPtr < matchFindInputLimit)
            {
                int currentInputOffset = (int)(inputPtr - inputBase);
                byte* windowStartPtr = inputBase + System.Math.Max(0, currentInputOffset - LZ4Constants.MaxOffset);

                // Find the longest match for the current input block
                Matcher.Match match = Matcher.FindLongestMatch(
                    hashTable,
                    inputBase,
                    inputPtr,
                    inputEnd - LZ4Constants.LastLiteralSize, // Ensure space for last literals
                    windowStartPtr,
                    currentInputOffset);

                if (!match.Found)
                {
                    // No match found, continue with literal run
                    inputPtr++;
                    continue;
                }

                // Match found - Emit Literal Run + Match Token
                int literalLength = (int)(inputPtr - literalStartPtr);
                int matchLength = match.Length;
                int offset = match.Offset;

                // Calculate the required space for this sequence
                int tokenLength = 1;
                int literalHeaderLength = (literalLength >= LZ4Constants.TokenLiteralMask)
                    ? 1 + (literalLength - LZ4Constants.TokenLiteralMask) / 255
                    : 0;

                int matchHeaderLength = (matchLength >= LZ4Constants.TokenMatchMask)
                    ? 1 + (matchLength - LZ4Constants.TokenMatchMask) / 255
                    : 0;

                int requiredSpace = tokenLength + literalHeaderLength + literalLength + sizeof(ushort) + matchHeaderLength;

                if (outputPtr + requiredSpace > outputEnd) return -1; // Output buffer too small

                // Write Token: Literal + Match info
                byte literalToken = (byte)System.Math.Min(literalLength, LZ4Constants.TokenLiteralMask);
                byte matchToken = (byte)System.Math.Min(matchLength - LZ4Constants.MinMatchLength, LZ4Constants.TokenMatchMask);
                byte token = (byte)((literalToken << 4) | matchToken);

                *outputPtr++ = token;

                // Write Literal Length (if needed)
                if (literalLength >= LZ4Constants.TokenLiteralMask)
                {
                    outputPtr += SpanKit.WriteVarInt(outputPtr, literalLength - LZ4Constants.TokenLiteralMask);
                }

                // Write Literals
                RawWriter.Write(ref outputPtr, literalStartPtr, literalLength);

                // Write Offset (Match distance)
                MemOps.WriteUnaligned<ushort>(outputPtr, (ushort)offset);
                outputPtr += sizeof(ushort);

                // Write Match Length (if needed)
                if (matchLength >= LZ4Constants.MinMatchLength + LZ4Constants.TokenMatchMask)
                {
                    outputPtr += SpanKit.WriteVarInt(outputPtr, matchLength - LZ4Constants.MinMatchLength - LZ4Constants.TokenMatchMask);
                }

                // Advance pointers
                inputPtr += matchLength;
                literalStartPtr = inputPtr; // Start new literal run
            }

            // Emit Last Literals
            int lastLiteralLength = (int)(inputEnd - literalStartPtr);
            if (lastLiteralLength > 0)
            {
                int tokenLength = 1;
                int literalHeaderLength = (lastLiteralLength >= LZ4Constants.TokenLiteralMask)
                    ? 1 + (lastLiteralLength - LZ4Constants.TokenLiteralMask) / 255
                    : 0;

                int requiredSpace = tokenLength + literalHeaderLength + lastLiteralLength;

                if (outputPtr + requiredSpace > outputEnd) return -1; // Output buffer too small

                byte literalToken = (byte)System.Math.Min(lastLiteralLength, LZ4Constants.TokenLiteralMask);
                byte token = (byte)(literalToken << 4); // Match part is 0
                *outputPtr++ = token;

                // Write Literal Length (if needed)
                if (lastLiteralLength >= LZ4Constants.TokenLiteralMask)
                {
                    outputPtr += SpanKit.WriteVarInt(outputPtr, lastLiteralLength - LZ4Constants.TokenLiteralMask);
                }

                // Write the literals
                RawWriter.Write(ref outputPtr, literalStartPtr, lastLiteralLength);
            }

            return (int)(outputPtr - outputBase); // Return length of compressed data (excluding header)
        }
    }
}
