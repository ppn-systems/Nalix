using Nalix.Shared.LZ4.Encoders;
using Nalix.Shared.LZ4.Internal;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Nalix.Shared.LZ4.Engine;

/// <summary>
/// Provides decompression functionality for the LZ4 format.
/// </summary>
internal readonly struct LZ4Decoder
{
    /// <summary>
    /// Decompresses the provided compressed data into the specified output buffer.
    /// </summary>
    /// <param name="input">The compressed data, including the header.</param>
    /// <param name="output">The buffer to store decompressed data. Size must match the original length in the header.</param>
    /// <returns>
    /// The number of bytes written to the output buffer (equal to the original length),
    /// or -1 if decompression fails.
    /// </returns>
    public static unsafe int Decode(System.ReadOnlySpan<byte> input, System.Span<byte> output)
    {
        // Validate header
        if (!TryReadHeader(input, out Header header) || output.Length < header.OriginalLength)
            return -1;

        if (header.OriginalLength == 0)
            return 0; // Successfully decompressed empty data

        fixed (byte* inputBase = &MemoryMarshal.GetReference(input))
        fixed (byte* outputBase = &MemoryMarshal.GetReference(output))
        {
            return DecodeInternal(header, inputBase, outputBase, input.Length);
        }
    }

    /// <summary>
    /// Decompresses the provided compressed data into a newly allocated output buffer.
    /// </summary>
    /// <param name="input">The compressed data, including the header.</param>
    /// <param name="output">The decompressed data, or <c>null</c> if decompression fails.</param>
    /// <param name="bytesWritten">The number of bytes written to the output buffer.</param>
    /// <returns><c>true</c> if decompression succeeds; otherwise, <c>false</c>.</returns>
    public static unsafe bool Decode(
        System.ReadOnlySpan<byte> input,
        [NotNullWhen(true)] out byte[]? output,
        out int bytesWritten)
    {
        output = null;
        bytesWritten = 0;

        // Validate header
        if (!TryReadHeader(input, out Header header) || input.Length != header.CompressedLength)
            return false;

        if (header.OriginalLength == 0)
        {
            output = [];
            return true; // Successfully decompressed empty data
        }

        // Allocate output buffer
        output = new byte[header.OriginalLength];

        fixed (byte* inputBase = &MemoryMarshal.GetReference(input))
        fixed (byte* outputBase = output)
        {
            bytesWritten = DecodeInternal(header, inputBase, outputBase, input.Length);
            if (bytesWritten < 0)
            {
                output = null;
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Reads the header from the compressed input.
    /// </summary>
    private static bool TryReadHeader(System.ReadOnlySpan<byte> input, out Header header)
    {
        header = default;

        if (input.Length < Header.Size)
            return false; // Input too small

        header = MemOps.ReadUnaligned<Header>(input);

        return header.OriginalLength >= 0;
    }

    /// <summary>
    /// Performs the core decompression logic.
    /// </summary>
    private static unsafe int DecodeInternal(Header header, byte* inputBase, byte* outputBase, int _)
    {
        byte* inputPtr = inputBase + Header.Size;
        byte* inputEnd = inputBase + header.CompressedLength;

        byte* outputPtr = outputBase;
        byte* outputEnd = outputBase + header.OriginalLength;

        while (inputPtr < inputEnd)
        {
            // --- Decode Token ---
            if (!TryReadByte(ref inputPtr, inputEnd, out byte token))
                return -1;

            // --- Decode and Copy Literals ---
            if (!TryDecodeLiterals(ref inputPtr, inputEnd, ref outputPtr, outputEnd, token))
                return -1;

            // Check if we reached the end
            if (inputPtr >= inputEnd || outputPtr >= outputEnd)
                break;

            // --- Decode Match ---
            if (!TryDecodeMatch(ref inputPtr, inputEnd, ref outputPtr, outputEnd, outputBase, token))
                return -1;
        }

        // Final validation
        return (outputPtr == outputEnd && inputPtr == inputEnd) ? header.OriginalLength : -1;
    }

    /// <summary>
    /// Reads a single byte from the input pointer.
    /// </summary>
    private static unsafe bool TryReadByte(ref byte* inputPtr, byte* inputEnd, out byte value)
    {
        value = 0;
        if (inputPtr >= inputEnd)
            return false;

        value = *inputPtr++;
        return true;
    }

    /// <summary>
    /// Decodes literal bytes and copies them to the output.
    /// </summary>
    private static unsafe bool TryDecodeLiterals(
        ref byte* inputPtr,
        byte* inputEnd,
        ref byte* outputPtr,
        byte* outputEnd,
        byte token)
    {
        int literalLength = (token >> 4) & LZ4Constants.TokenLiteralMask;
        if (literalLength == LZ4Constants.TokenLiteralMask)
        {
            if (!(SpanKit.ReadVarInt(ref inputPtr, inputEnd, out int extraLength) == -1) || extraLength < 0)
                return false;

            literalLength += extraLength;
        }

        if (inputPtr + literalLength > inputEnd || outputPtr + literalLength > outputEnd)
            return false;

        MemOps.Copy(inputPtr, outputPtr, literalLength);
        inputPtr += literalLength;
        outputPtr += literalLength;

        return true;
    }

    /// <summary>
    /// Decodes a match (offset and length) and copies the matching bytes to the output.
    /// </summary>
    private static unsafe bool TryDecodeMatch(
        ref byte* inputPtr,
        byte* inputEnd,
        ref byte* outputPtr,
        byte* outputEnd,
        byte* outputBase,
        byte token)
    {
        if (inputPtr + sizeof(ushort) > inputEnd)
            return false;

        int offset = MemOps.ReadUnaligned<ushort>(inputPtr);
        inputPtr += sizeof(ushort);

        if (offset == 0 || offset > (outputPtr - outputBase))
            return false;

        int matchLength = token & LZ4Constants.TokenMatchMask;
        if (matchLength == LZ4Constants.TokenMatchMask)
        {
            if (!(SpanKit.ReadVarInt(ref inputPtr, inputEnd, out int extraLength) == -1) || extraLength < 0)
                return false;

            matchLength += extraLength;
        }
        matchLength += LZ4Constants.MinMatchLength;

        if (outputPtr + matchLength > outputEnd)
            return false;

        byte* matchSourcePtr = outputPtr - offset;
        MemOps.Copy(matchSourcePtr, outputPtr, matchLength);
        outputPtr += matchLength;

        return true;
    }
}
