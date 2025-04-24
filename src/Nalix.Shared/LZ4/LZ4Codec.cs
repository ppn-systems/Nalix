using Nalix.Shared.LZ4.Encoders;
using Nalix.Shared.LZ4.Internal;
using System.Runtime.InteropServices;

namespace Nalix.Shared.LZ4;

/// <summary>
/// Provides functionality for compressing and decompressing data using the Nalix compression algorithm.
/// The API is static and struct-based to minimize allocations and provide high performance.
/// </summary>
public unsafe class LZ4Codec
{
    /// <summary>
    /// Compresses the provided input data into the specified output buffer.
    /// The compression is done using the Nalix algorithm with zero-allocation and high efficiency.
    /// </summary>
    /// <param name="input">The input data to compress. This must be a ReadOnlySpan of bytes.</param>
    /// <param name="output">The buffer into which the compressed data will be written. The buffer must have sufficient capacity to hold the compressed data, including the header.</param>
    /// <returns>The total number of bytes written to the output buffer, including the header.
    /// Returns -1 if the output buffer is too small or an error occurs during compression.</returns>
    /// <remarks>
    /// If the input data is empty, only the header will be written to the output buffer.
    /// If compression fails (e.g., the output buffer is too small), -1 is returned.
    /// </remarks>
    public static int Compress(System.ReadOnlySpan<byte> input, System.Span<byte> output)
    {
        if (input.IsEmpty)
        {
            if (output.Length < Header.Size) return -1; // Need space for header even if input is empty
            // Write header for empty input
            var header = new Header(0, Header.Size);
            MemOps.WriteUnaligned(output, header);
            return Header.Size;
        }

        // Ensure output is large enough for at least the header
        if (output.Length < Header.Size) return -1;

        int* hashTable = stackalloc int[Matcher.HashTableSize]; // ~256KB for 64k entries

        // Clear the hash table (important!)
        new System.Span<byte>(hashTable, Matcher.MaxStackallocHashTableSize).Clear();

        // Slice output buffer to exclude header space initially
        System.Span<byte> compressedDataOutput = output[Header.Size..];

        int compressedDataLength = FastPath.EncodeBlock(input, compressedDataOutput, hashTable);

        // Compression failed (likely output buffer too small)
        if (compressedDataLength < 0) return -1;

        int totalCompressedLength = Header.Size + compressedDataLength;

        // Write the final header
        var finalHeader = new Header(input.Length, totalCompressedLength);
        MemOps.WriteUnaligned(output, finalHeader); // Write to the beginning of the original output span

        return totalCompressedLength;
    }

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
    public static int Decompress(System.ReadOnlySpan<byte> input, System.Span<byte> output)
    {
        if (input.Length < Header.Size) return -1; // Input too small to contain header

        // Read Header
        Header header = MemOps.ReadUnaligned<Header>(input);

        if (header.OriginalLength < 0 || header.CompressedLength != input.Length) return -1; // Invalid header
        if (output.Length != header.OriginalLength) return -1; // Output buffer has incorrect size

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
                int literalLength = (token >> 4) & Constants.TokenLiteralMask;
                if (literalLength == Constants.TokenLiteralMask)
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
                int matchLength = token & Constants.TokenMatchMask;
                if (matchLength == Constants.TokenMatchMask)
                {
                    int lenRead = SpanKit.ReadVarInt(ref inputPtr, inputEnd, out int extraLength);
                    if (extraLength < 0) return -1; // Read error

                    matchLength += extraLength;
                }

                // Length is stored relative to min match length
                matchLength += Constants.MinMatchLength;

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
}
