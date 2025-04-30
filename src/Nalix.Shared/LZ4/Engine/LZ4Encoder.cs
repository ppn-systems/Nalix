using Nalix.Shared.LZ4.Internal;

namespace Nalix.Shared.LZ4.Engine;

/// <summary>
/// Provides functionality to compress data using the LZ4 algorithm, optimized for zero-allocation and high efficiency.
/// </summary>
internal readonly struct LZ4Encoder
{
    /// <summary>
    /// Compresses the provided input data into the specified output buffer.
    /// </summary>
    /// <param name="input">The input data to compress as a <see cref="System.ReadOnlySpan{T}"/>.</param>
    /// <param name="output">The buffer where the compressed data will be written. Must have enough capacity.</param>
    /// <returns>
    /// The total number of bytes written to the output buffer (including the header),
    /// or -1 if the output buffer is too small or compression fails.
    /// </returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe int Encode(
        System.ReadOnlySpan<byte> input,
        System.Span<byte> output)
    {
        // Handle empty input
        if (input.IsEmpty)
            return WriteEmptyHeader(output);

        // Ensure space for at least the header
        if (output.Length < Header.Size)
            return -1;

        // Allocate hash table for compression
        int* hashTable = stackalloc int[Matcher.HashTableSize];
        InitializeHashTable(hashTable);

        // Compress the data
        System.Span<byte> compressedDataOutput = output[Header.Size..];
        int compressedDataLength = Encoders.LZ4Encoder.EncodeBlock(input, compressedDataOutput, hashTable);

        // Handle compression failure
        if (compressedDataLength < 0)
            return -1;

        // Write the header and return total compressed length
        int totalCompressedLength = Header.Size + compressedDataLength;
        WriteHeader(output, input.Length, totalCompressedLength);

        return totalCompressedLength;
    }

    /// <summary>
    /// Compresses the provided input data into a specified output buffer with a success flag.
    /// </summary>
    /// <param name="input">The input data to compress as a <see cref="System.ReadOnlySpan{T}"/>.</param>
    /// <param name="output">The buffer where the compressed data will be written. Must have enough capacity.</param>
    /// <param name="bytesWritten">The total number of bytes written to the output buffer.</param>
    /// <returns><c>true</c> if compression succeeds; otherwise, <c>false</c>.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe bool Encode(
        System.ReadOnlySpan<byte> input,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] System.Span<byte> output,
        out int bytesWritten)
    {
        bytesWritten = 0;

        // Handle empty input
        if (input.IsEmpty)
        {
            if (WriteEmptyHeader(output) == -1)
                return false;

            bytesWritten = Header.Size;
            return true;
        }

        // Ensure space for at least the header
        if (output.Length < Header.Size)
            return false;

        // Allocate hash table for compression
        int* hashTable = stackalloc int[Matcher.HashTableSize];
        InitializeHashTable(hashTable);

        // Compress the data
        System.Span<byte> compressedDataOutput = output[Header.Size..];
        int compressedDataLength = Encoders.LZ4Encoder.EncodeBlock(input, compressedDataOutput, hashTable);

        // Handle compression failure
        if (compressedDataLength < 0)
            return false;

        // Write the header and calculate total compressed length
        int totalCompressedLength = Header.Size + compressedDataLength;
        WriteHeader(output, input.Length, totalCompressedLength);

        bytesWritten = totalCompressedLength;
        return true;
    }

    /// <summary>
    /// Initializes the hash table to zero.
    /// </summary>
    /// <param name="hashTable">A pointer to the hash table.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static unsafe void InitializeHashTable(int* hashTable)
        => new System.Span<byte>(hashTable, Matcher.MaxStackallocHashTableSize).Clear();

    /// <summary>
    /// Writes a header for an empty input to the output buffer.
    /// </summary>
    /// <param name="output">The output buffer to write the header into.</param>
    /// <returns>The size of the header, or -1 if the buffer is too small.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static int WriteEmptyHeader(System.Span<byte> output)
    {
        if (output.Length < Header.Size)
            return -1;

        Header header = new(0, Header.Size);
        MemOps.WriteUnaligned(output, header);
        return Header.Size;
    }

    /// <summary>
    /// Writes the header to the output buffer.
    /// </summary>
    /// <param name="output">The output buffer to write the header into.</param>
    /// <param name="originalLength">The original length of the input data.</param>
    /// <param name="compressedLength">The total compressed length, including the header.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void WriteHeader(System.Span<byte> output, int originalLength, int compressedLength)
    {
        Header header = new(originalLength, compressedLength);
        MemOps.WriteUnaligned(output, header);
    }
}
