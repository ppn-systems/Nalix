using Nalix.Shared.LZ4.Encoders;
using Nalix.Shared.LZ4.Internal;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Nalix.Shared.LZ4.Engine;

internal readonly struct LZ4Decoder
{
    /// <summary>
    /// Decompresses the provided compressed data into the specified output buffer.
    /// The input data must include a valid header that specifies the original length and compressed length.
    /// </summary>
    /// <param name="input">The compressed data, including the header, that needs to be decompressed.</param>
    /// <param name="output">The buffer into which the decompressed data will be written. The buffer must have the exact size specified in the header's original length.</param>
    /// <returns>The number of bytes written to the output buffer, which should be the original length as specified in the header.
    /// Returns -1 if decompression fails (e.g., corrupted data or incorrect output buffer size).</returns>
    /// <remarks>
    /// The method performs bounds checks to ensure the integrity of the decompressed data.
    /// It will return -1 if the decompressed data does not match the expected size or if the input is corrupted.
    /// </remarks>
    public static unsafe int Decode(System.ReadOnlySpan<byte> input, System.Span<byte> output)
    {
        if (input.Length < Header.Size) return -1; // Input too small to contain header

        // Read Header
        Header header = MemOps.ReadUnaligned<Header>(input);

        if (header.OriginalLength < 0) return -1; // Invalid header
        if (output.Length < header.OriginalLength) return -1; // Output buffer has incorrect size

        if (header.OriginalLength == 0)
        {
            return 0; // Empty stream successfully decompressed
        }

        fixed (byte* inputBase = &MemoryMarshal.GetReference(input))
        fixed (byte* outputBase = &MemoryMarshal.GetReference(output))
        {
            byte* inputPtr = inputBase + Header.Size;
            byte* inputEnd = inputBase + header.CompressedLength; // End of compressed data

            byte* outputPtr = outputBase;
            byte* outputEnd = outputBase + header.OriginalLength;

            while (inputPtr < inputEnd)
            {
                // --- Read Token ---
                if (inputPtr >= inputEnd) return -1; // Unexpected end of input
                byte token = *inputPtr++;

                // --- Decode and Copy Literals ---
                int literalLength = (token >> 4) & LZ4Constants.TokenLiteralMask;
                if (literalLength == LZ4Constants.TokenLiteralMask)
                {
                    int lenRead = SpanKit.ReadVarInt(ref inputPtr, inputEnd, out int extraLength);
                    if (extraLength < 0) return -1; // Read error
                    literalLength += extraLength;
                }

                if (literalLength > 0)
                {
                    if (inputPtr + literalLength > inputEnd || outputPtr + literalLength > outputEnd) return -1; // Bounds check
                    MemOps.Copy(inputPtr, outputPtr, literalLength);
                    inputPtr += literalLength;
                    outputPtr += literalLength;
                }

                // Check if we reached the end after literals (last sequence might only have literals)
                if (inputPtr >= inputEnd || outputPtr >= outputEnd) break;

                // --- Decode Match Offset ---
                if (inputPtr + sizeof(ushort) > inputEnd) return -1; // Need space for offset
                int offset = MemOps.ReadUnaligned<ushort>(inputPtr);
                inputPtr += sizeof(ushort);
                if (offset == 0 || offset > (outputPtr - outputBase)) return -1; // Invalid offset

                // --- Decode Match Length ---
                int matchLength = token & LZ4Constants.TokenMatchMask;
                if (matchLength == LZ4Constants.TokenMatchMask)
                {
                    int lenRead = SpanKit.ReadVarInt(ref inputPtr, inputEnd, out int extraLength);
                    if (extraLength < 0) return -1; // Read error

                    matchLength += extraLength;
                }

                // Length is stored relative to min match length
                matchLength += LZ4Constants.MinMatchLength;

                // --- Copy Match ---
                byte* matchSourcePtr = outputPtr - offset;
                if (outputPtr + matchLength > outputEnd) return -1; // Output bounds check

                // MemOps.Copy handles the overlapping case needed here (dest > src)
                MemOps.Copy(matchSourcePtr, outputPtr, matchLength);
                outputPtr += matchLength;
            }

            // Final check: Did we decompress the exact amount expected?
            // Decompression produced wrong size or didn't consume all input
            if (outputPtr != outputEnd || inputPtr != inputEnd)
                return -1;

            return header.OriginalLength;
        }
    }

    public static unsafe bool Decode(
        System.ReadOnlySpan<byte> input,
        [NotNullWhen(true)] out byte[]? output, out int bytesWritten)
    {
        output = null;
        bytesWritten = 0;

        if (input.Length < Header.Size)
            return false; // Input too small to contain header

        // Read Header
        Header header = MemOps.ReadUnaligned<Header>(input);

        if (header.OriginalLength < 0 || header.CompressedLength != input.Length)
            return false; // Invalid header

        if (header.OriginalLength == 0)
        {
            output = [];
            bytesWritten = 0;
            return true; // Empty stream successfully decompressed
        }

        // Allocate output buffer
        output = new byte[header.OriginalLength];

        fixed (byte* inputBase = &MemoryMarshal.GetReference(input))
        fixed (byte* outputBase = output)
        {
            byte* inputPtr = inputBase + Header.Size;
            byte* inputEnd = inputBase + header.CompressedLength;

            byte* outputPtr = outputBase;
            byte* outputEnd = outputBase + header.OriginalLength;

            while (inputPtr < inputEnd)
            {
                // --- Read Token ---
                if (inputPtr >= inputEnd) return false;
                byte token = *inputPtr++;

                // --- Decode and Copy Literals ---
                int literalLength = (token >> 4) & LZ4Constants.TokenLiteralMask;
                if (literalLength == LZ4Constants.TokenLiteralMask)
                {
                    int lenRead = SpanKit.ReadVarInt(ref inputPtr, inputEnd, out int extraLength);
                    if (extraLength < 0) return false;
                    literalLength += extraLength;
                }

                if (literalLength > 0)
                {
                    if (inputPtr + literalLength > inputEnd || outputPtr + literalLength > outputEnd)
                        return false;

                    MemOps.Copy(inputPtr, outputPtr, literalLength);
                    inputPtr += literalLength;
                    outputPtr += literalLength;
                }

                if (inputPtr >= inputEnd || outputPtr >= outputEnd)
                    break;

                // --- Decode Match Offset ---
                if (inputPtr + sizeof(ushort) > inputEnd)
                    return false;

                int offset = MemOps.ReadUnaligned<ushort>(inputPtr);
                inputPtr += sizeof(ushort);
                if (offset == 0 || offset > (outputPtr - outputBase))
                    return false;

                // --- Decode Match Length ---
                int matchLength = token & LZ4Constants.TokenMatchMask;
                if (matchLength == LZ4Constants.TokenMatchMask)
                {
                    int lenRead = SpanKit.ReadVarInt(ref inputPtr, inputEnd, out int extraLength);
                    if (extraLength < 0) return false;
                    matchLength += extraLength;
                }
                matchLength += LZ4Constants.MinMatchLength;

                // --- Copy Match ---
                byte* matchSourcePtr = outputPtr - offset;
                if (outputPtr + matchLength > outputEnd)
                    return false;

                MemOps.Copy(matchSourcePtr, outputPtr, matchLength);
                outputPtr += matchLength;
            }

            // Final check
            if (outputPtr != outputEnd || inputPtr != inputEnd)
                return false;

            bytesWritten = header.OriginalLength;
            return true;
        }
    }
}
