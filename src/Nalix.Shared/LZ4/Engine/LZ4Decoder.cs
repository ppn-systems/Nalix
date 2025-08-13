using Nalix.Shared.LZ4.Encoders;
using Nalix.Shared.Memory.Unsafe;

namespace Nalix.Shared.LZ4.Engine;

/// <summary>
/// Provides decompression functionality for the LZ4 format.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
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
    /// </returns>26
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Int32 Decode(
        System.ReadOnlySpan<System.Byte> input,
        System.Span<System.Byte> output) => !DecodeInternal(input, output, out System.Int32 written) ? -1 : written;

    /// <summary>
    /// Decompresses the provided compressed data into a newly allocated output buffer.
    /// </summary>
    /// <param name="input">The compressed data, including the header.</param>
    /// <param name="output">The decompressed data, or <c>null</c> if decompression fails.</param>
    /// <param name="bytesWritten">The number of bytes written to the output buffer.</param>
    /// <returns><c>true</c> if decompression succeeds; otherwise, <c>false</c>.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Boolean Decode(
        System.ReadOnlySpan<System.Byte> input,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out System.Byte[]? output,
        out System.Int32 bytesWritten)
    {
        output = null;
        bytesWritten = 0;

        if (input.Length < LZ4BlockHeader.Size)
        {
            return false;
        }

        LZ4BlockHeader header = MemOps.ReadUnaligned<LZ4BlockHeader>(input);
        if (header.OriginalLength < 0 || header.CompressedLength != input.Length)
        {
            return false;
        }

        if (header.OriginalLength == 0)
        {
            output = [];
            return true;
        }

        output = new System.Byte[header.OriginalLength];
        return DecodeInternal(input, output, out bytesWritten);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static unsafe System.Boolean DecodeInternal(
        System.ReadOnlySpan<System.Byte> input,
        System.Span<System.Byte> output,
        out System.Int32 bytesWritten)
    {
        bytesWritten = 0;

        if (input.Length < LZ4BlockHeader.Size)
        {
            return false;
        }

        LZ4BlockHeader header = MemOps.ReadUnaligned<LZ4BlockHeader>(input);
        if (header.OriginalLength != output.Length || header.OriginalLength < 0)
        {
            return false;
        }

        if (header.OriginalLength == 0)
        {
            return true;
        }

        fixed (System.Byte* inputBase = &System.Runtime.InteropServices.MemoryMarshal.GetReference(input))
        fixed (System.Byte* outputBase = &System.Runtime.InteropServices.MemoryMarshal.GetReference(output))
        {
            System.Byte* inputPtr = inputBase + LZ4BlockHeader.Size;
            System.Byte* inputEnd = inputBase + header.CompressedLength;
            System.Byte* outputPtr = outputBase;
            System.Byte* outputEnd = outputBase + header.OriginalLength;

            while (inputPtr < inputEnd)
            {
                if (inputPtr >= inputEnd)
                {
                    return false;
                }

                System.Byte token = *inputPtr++;

                System.Int32 literalLength = (token >> 4) & LZ4CompressionConstants.TokenLiteralMask;

                if (literalLength == LZ4CompressionConstants.TokenLiteralMask)
                {
                    System.Int32 bytesRead = SpanOps.ReadVarInt(ref inputPtr, inputEnd, out System.Int32 extraLength);
                    if (bytesRead == -1 || extraLength < 0)
                    {
                        return false;
                    }

                    literalLength += extraLength;
                }

                if (literalLength > 0)
                {
                    if (inputPtr + literalLength > inputEnd || outputPtr + literalLength > outputEnd)
                    {
                        return false;
                    }

                    MemOps.Copy(inputPtr, outputPtr, literalLength);
                    inputPtr += literalLength;
                    outputPtr += literalLength;
                }

                if (inputPtr >= inputEnd || outputPtr >= outputEnd)
                {
                    break;
                }

                if (inputPtr + sizeof(System.UInt16) > inputEnd)
                {
                    return false;
                }

                System.Int32 offset = MemOps.ReadUnaligned<System.UInt16>(inputPtr);
                inputPtr += sizeof(System.UInt16);
                if (offset == 0 || offset > (outputPtr - outputBase))
                {
                    return false;
                }

                System.Int32 matchLength = token & LZ4CompressionConstants.TokenMatchMask;
                if (matchLength == LZ4CompressionConstants.TokenMatchMask)
                {
                    System.Int32 bytesRead = SpanOps.ReadVarInt(ref inputPtr, inputEnd, out System.Int32 extraLength);
                    if (bytesRead == -1 || extraLength < 0)
                    {
                        return false;
                    }

                    matchLength += extraLength;
                }
                matchLength += LZ4CompressionConstants.MinMatchLength;

                System.Byte* matchSourcePtr = outputPtr - offset;
                if (outputPtr + matchLength > outputEnd)
                {
                    return false;
                }

                MemOps.Copy(matchSourcePtr, outputPtr, matchLength);
                outputPtr += matchLength;
            }

            if (outputPtr != outputEnd || inputPtr != inputEnd)
            {
                return false;
            }

            bytesWritten = header.OriginalLength;
            return true;
        }
    }
}
