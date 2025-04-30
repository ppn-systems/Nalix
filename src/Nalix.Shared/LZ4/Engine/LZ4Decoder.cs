using Nalix.Shared.LZ4.Encoders;
using Nalix.Shared.LZ4.Internal;

namespace Nalix.Shared.LZ4.Engine;

/// <summary>
/// Provides decompression functionality for the LZ4 format.
/// </summary>
public readonly struct LZ4Decoder
{
    /// <summary>
    /// Decompresses the provided compressed data into the specified output buffer.
    /// </summary>
    /// <param name="input">The compressed data, including the header.</param>
    /// <param name="output">The buffer to store decompressed data. Size must match the original length in the header.</param>
    /// <returns>
    /// The number of bytes written to the output buffer (equal to the original length),
    /// or -1 if decompression fails.
    /// </returns>26
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static int Decode(System.ReadOnlySpan<byte> input, System.Span<byte> output)
    {
        if (!DecodeInternal(input, output, out int written)) return -1;
        return written;
    }

    /// <summary>
    /// Decompresses the provided compressed data into a newly allocated output buffer.
    /// </summary>
    /// <param name="input">The compressed data, including the header.</param>
    /// <param name="output">The decompressed data, or <c>null</c> if decompression fails.</param>
    /// <param name="bytesWritten">The number of bytes written to the output buffer.</param>
    /// <returns><c>true</c> if decompression succeeds; otherwise, <c>false</c>.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static bool Decode(
        System.ReadOnlySpan<byte> input,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out byte[]? output,
        out int bytesWritten)
    {
        output = null;
        bytesWritten = 0;

        if (input.Length < Header.Size)
            return false;

        Header header = MemOps.ReadUnaligned<Header>(input);
        if (header.OriginalLength < 0 || header.CompressedLength != input.Length)
            return false;

        if (header.OriginalLength == 0)
        {
            output = [];
            return true;
        }

        output = new byte[header.OriginalLength];
        return DecodeInternal(input, output, out bytesWritten);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static unsafe bool DecodeInternal(
        System.ReadOnlySpan<byte> input,
        System.Span<byte> output,
        out int bytesWritten)
    {
        bytesWritten = 0;

        if (input.Length < Header.Size)
            return false;

        Header header = MemOps.ReadUnaligned<Header>(input);
        if (header.OriginalLength != output.Length || header.OriginalLength < 0)
            return false;

        if (header.OriginalLength == 0)
            return true;

        fixed (byte* inputBase = &System.Runtime.InteropServices.MemoryMarshal.GetReference(input))
        fixed (byte* outputBase = &System.Runtime.InteropServices.MemoryMarshal.GetReference(output))
        {
            byte* inputPtr = inputBase + Header.Size;
            byte* inputEnd = inputBase + header.CompressedLength;
            byte* outputPtr = outputBase;
            byte* outputEnd = outputBase + header.OriginalLength;

            while (inputPtr < inputEnd)
            {
                if (inputPtr >= inputEnd) return false;
                byte token = *inputPtr++;

                int literalLength = (token >> 4) & LZ4Constants.TokenLiteralMask;

                if (literalLength == LZ4Constants.TokenLiteralMask)
                {
                    int bytesRead = SpanOps.ReadVarInt(ref inputPtr, inputEnd, out int extraLength);
                    if (bytesRead == -1 || extraLength < 0)
                        return false;

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

                if (inputPtr + sizeof(ushort) > inputEnd)
                    return false;

                int offset = MemOps.ReadUnaligned<ushort>(inputPtr);
                inputPtr += sizeof(ushort);
                if (offset == 0 || offset > (outputPtr - outputBase))
                    return false;

                int matchLength = token & LZ4Constants.TokenMatchMask;
                if (matchLength == LZ4Constants.TokenMatchMask)
                {
                    int bytesRead = SpanOps.ReadVarInt(ref inputPtr, inputEnd, out int extraLength);
                    if (bytesRead == -1 || extraLength < 0)
                        return false;

                    matchLength += extraLength;
                }
                matchLength += LZ4Constants.MinMatchLength;

                byte* matchSourcePtr = outputPtr - offset;
                if (outputPtr + matchLength > outputEnd)
                    return false;

                MemOps.Copy(matchSourcePtr, outputPtr, matchLength);
                outputPtr += matchLength;
            }

            if (outputPtr != outputEnd || inputPtr != inputEnd)
                return false;

            bytesWritten = header.OriginalLength;
            return true;
        }
    }
}
