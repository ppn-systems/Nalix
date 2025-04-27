using Nalix.Shared.LZ4.Encoders;
using Nalix.Shared.LZ4.Internal;

namespace Nalix.Shared.LZ4.Engine;

internal readonly struct LZ4Encoder
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
    public static unsafe int Encode(
        System.ReadOnlySpan<byte> input,
        System.Span<byte> output)
    {
        if (input.IsEmpty)
        {
            // Need space for header even if input is empty
            if (output.Length < Header.Size)
                return -1;

            // Write header for empty input
            Header header = new(0, Header.Size);
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
        Header finalHeader = new(input.Length, totalCompressedLength);
        MemOps.WriteUnaligned(output, finalHeader); // Write to the beginning of the original output span

        return totalCompressedLength;
    }
}
