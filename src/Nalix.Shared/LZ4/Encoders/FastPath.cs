using Nalix.Shared.LZ4.Internal;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Nalix.Shared.LZ4.Encoders;

/// <summary>
/// Implements the main greedy compression path (LZ4 style).
/// </summary>
internal static unsafe class FastPath
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int EncodeBlock(
        System.ReadOnlySpan<byte> input,
        System.Span<byte> output,
        int* hashTable) // Pointer to stackalloc'd or pooled hash table
    {
        fixed (byte* inputBase = &MemoryMarshal.GetReference(input))
        fixed (byte* outputBase = &MemoryMarshal.GetReference(output))
        {
            byte* inputPtr = inputBase;
            byte* inputEnd = inputBase + input.Length;
            // Leave room for last literal run + match check
            byte* matchFindInputLimit = inputEnd - Constants.LastLiteralSize - Constants.MinMatchLength;

            byte* outputPtr = outputBase;
            byte* outputEnd = outputBase + output.Length;

            byte* literalStartPtr = inputBase; // Start of the current literal run

            // Ensure output has minimum space for header + potential minimal data
            if (output.Length < Header.Size + 1) return -1;

            // Main loop
            while (inputPtr < matchFindInputLimit)
            {
                int currentInputOffset = (int)(inputPtr - inputBase);
                byte* windowStartPtr = inputBase + System.Math.Max(0, currentInputOffset - Constants.MaxOffset);

                Matcher.Match match = Matcher.FindLongestMatch(
                    hashTable,
                    inputBase,
                    inputPtr,
                    inputEnd - Constants.LastLiteralSize, // Ensure space for last literals
                    windowStartPtr,
                    currentInputOffset);

                if (!match.Found)
                {
                    inputPtr++; // Continue literal run
                    continue;
                }

                // Match Found - Emit Literal Run + Match Token
                int literalLength = (int)(inputPtr - literalStartPtr);
                int matchLength = match.Length;
                int offset = match.Offset;

                // Calculate space needed for this sequence
                int tokenLength = 1;
                int literalHeaderLength = (literalLength >= Constants.TokenLiteralMask) ? 1 + (literalLength - Constants.TokenLiteralMask) / 255 : 0;
                int matchHeaderLength = (matchLength >= Constants.TokenMatchMask) ? 1 + (matchLength - Constants.TokenMatchMask) / 255 : 0;
                int requiredSpace = tokenLength + literalHeaderLength + literalLength + sizeof(ushort) + matchHeaderLength;

                if (outputPtr + requiredSpace > outputEnd) return -1; // Output buffer too small

                // --- Write Token ---
                byte literalToken = (byte)System.Math.Min(literalLength, Constants.TokenLiteralMask);

                // Match len stored relative to MinMatchLength
                byte matchToken = (byte)System.Math.Min(matchLength - Constants.MinMatchLength, Constants.TokenMatchMask);
                byte token = (byte)((literalToken << 4) | matchToken);
                *outputPtr++ = token;

                // --- Write Literal Length (if needed) ---
                if (literalLength >= Constants.TokenLiteralMask)
                {
                    outputPtr += SpanKit.WriteVarInt(outputPtr, literalLength - Constants.TokenLiteralMask);
                }

                // --- Write Literals ---
                RawWriter.Write(ref outputPtr, literalStartPtr, literalLength);

                // --- Write Offset ---
                MemOps.WriteUnaligned<ushort>(outputPtr, (ushort)offset); // Assuming offset <= 65535
                outputPtr += sizeof(ushort);

                // --- Write Match Length (if needed) ---
                if (matchLength >= Constants.MinMatchLength + Constants.TokenMatchMask)
                {
                    outputPtr += SpanKit.WriteVarInt(outputPtr, matchLength - Constants.MinMatchLength - Constants.TokenMatchMask);
                }

                // --- Advance Pointers ---
                inputPtr += matchLength;
                literalStartPtr = inputPtr; // Start new literal run
            }

            // --- Emit Last Literals ---
            int lastLiteralLength = (int)(inputEnd - literalStartPtr);
            if (lastLiteralLength > 0)
            {
                int tokenLength = 1;
                int literalHeaderLength = (lastLiteralLength >= Constants.TokenLiteralMask) ? 1 + (lastLiteralLength - Constants.TokenLiteralMask) / 255 : 0;
                int requiredSpace = tokenLength + literalHeaderLength + lastLiteralLength;

                if (outputPtr + requiredSpace > outputEnd) return -1; // Output buffer too small

                byte literalToken = (byte)System.Math.Min(lastLiteralLength, Constants.TokenLiteralMask);
                byte token = (byte)(literalToken << 4); // Match part is 0
                *outputPtr++ = token;

                if (lastLiteralLength >= Constants.TokenLiteralMask)
                {
                    outputPtr += SpanKit.WriteVarInt(outputPtr, lastLiteralLength - Constants.TokenLiteralMask);
                }

                RawWriter.Write(ref outputPtr, literalStartPtr, lastLiteralLength);
            }

            return (int)(outputPtr - outputBase); // Return length of compressed data (excluding header)
        }
    }
}
