// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Shared.LZ4.Encoders;
using Nalix.Shared.Memory.Buffers;
using Nalix.Shared.Memory.Internal;

namespace Nalix.Shared.LZ4.Engine;

/// <summary>
/// Provides decompression functionality for the LZ4 format.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
internal static class LZ4Decoder
{
    #region APIs

    /// <summary>
    /// Decompresses the provided compressed data into the specified output buffer.
    /// </summary>
    /// <param name="input">The compressed data, including the header.</param>
    /// <param name="output">The buffer to store decompressed data. Size must match the original length in the header.</param>
    /// <returns>
    /// The number of bytes written to the output buffer (equal to the original length),
    /// or -1 if decompression fails.
    /// </returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static int Decode(
        System.ReadOnlySpan<byte> input,
        System.Span<byte> output) => !DecodeInternal(input, output, out int written) ? -1 : written;

    /// <summary>
    /// Decompresses the provided compressed data into a newly allocated output buffer.
    /// </summary>
    /// <param name="input">The compressed data, including the header.</param>
    /// <param name="output">The decompressed data, or <c>null</c> if decompression fails.</param>
    /// <param name="bytesWritten">The number of bytes written to the output buffer.</param>
    /// <returns><c>true</c> if decompression succeeds; otherwise, <c>false</c>.</returns>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static bool Decode(
        System.ReadOnlySpan<byte> input,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out byte[]? output,
        [System.Diagnostics.CodeAnalysis.NotNull] out int bytesWritten)
    {
        output = null;
        bytesWritten = 0;

        if (!TryReadAndValidateHeader(input, out LZ4BlockHeader header))
        {
            return false;
        }

        if (header.OriginalLength == 0)
        {
            output = [];
            return true;
        }

        output = new byte[header.OriginalLength];
        if (!DecodeInternal(input, output, out bytesWritten))
        {
            output = null;
            bytesWritten = 0;
            return false;
        }
        return true;
    }

    /// <summary>
    /// Decompresses the provided compressed data into a <see cref="BufferLease"/> rented from the pool.
    /// Caller is responsible for disposing the lease when done.
    /// </summary>
    /// <param name="input">The compressed data, including the header.</param>
    /// <param name="lease">
    /// On success, a <see cref="BufferLease"/> whose <see cref="BufferLease.Span"/> contains
    /// exactly <c>bytesWritten</c> bytes of decompressed data. Must be disposed by the caller.
    /// On failure, <c>null</c>.
    /// </param>
    /// <param name="bytesWritten">The number of bytes written to the lease.</param>
    /// <returns><c>true</c> if decompression succeeds; otherwise, <c>false</c>.</returns>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static bool Decode(
        System.ReadOnlySpan<byte> input,
        out BufferLease? lease,
        [System.Diagnostics.CodeAnalysis.NotNull] out int bytesWritten)
    {
        lease = null;
        bytesWritten = 0;

        if (!TryReadAndValidateHeader(input, out LZ4BlockHeader header))
        {
            return false;
        }

        // OriginalLength == 0: data rỗng hợp lệ — lease = null, bytesWritten = 0, return true
        if (header.OriginalLength == 0)
        {
            return true;
        }

        BufferLease rentedLease = BufferLease.Rent(header.OriginalLength);

        if (!DecodeInternal(input, rentedLease.SpanFull, out bytesWritten))
        {
            rentedLease.Dispose();
            return false;
        }

        rentedLease.CommitLength(bytesWritten);
        lease = rentedLease;
        return true;
    }

    #endregion APIs

    #region Private Methods

    /// <summary>
    /// Validates input length and reads the LZ4 block header.
    /// Centralises header checks shared by all three Decode overloads.
    /// </summary>
    /// <param name="input"></param>
    /// <param name="header"></param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static bool TryReadAndValidateHeader(
        System.ReadOnlySpan<byte> input,
        out LZ4BlockHeader header)
    {
        header = default;

        if (input.Length < LZ4BlockHeader.Size)
        {
            return false;
        }

        header = MemOps.ReadUnaligned<LZ4BlockHeader>(input);

        return header.OriginalLength >= 0
            && header.CompressedLength >= LZ4BlockHeader.Size
            && header.CompressedLength == input.Length
            && header.OriginalLength <= LZ4CompressionConstants.MaxBlockSize;
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static unsafe bool DecodeInternal(
        System.ReadOnlySpan<byte> input,
        System.Span<byte> output,
        [System.Diagnostics.CodeAnalysis.NotNull] out int bytesWritten)
    {
        bytesWritten = 0;

        if (input.Length < LZ4BlockHeader.Size)
        {
            return false;
        }

        LZ4BlockHeader header = MemOps.ReadUnaligned<LZ4BlockHeader>(input);
        if (header.OriginalLength > output.Length || header.OriginalLength < 0)
        {
            return false;
        }

        if (header.OriginalLength == 0)
        {
            return true;
        }

        fixed (byte* inputBase = &System.Runtime.InteropServices.MemoryMarshal.GetReference(input))
        {
            fixed (byte* outputBase = &System.Runtime.InteropServices.MemoryMarshal.GetReference(output))
            {
                byte* inputPtr = inputBase + LZ4BlockHeader.Size;
                byte* inputEnd = inputBase + header.CompressedLength;
                byte* outputPtr = outputBase;
                byte* outputEnd = outputBase + header.OriginalLength;

                while (inputPtr < inputEnd)
                {
                    byte token = *inputPtr++;

                    int literalLength = (token >> 4) & LZ4CompressionConstants.TokenLiteralMask;

                    if (literalLength == LZ4CompressionConstants.TokenLiteralMask)
                    {
                        int bytesRead = SpanOps.ReadVarInt(ref inputPtr, inputEnd, out int extraLength);
                        if (bytesRead == -1 || extraLength < 0)
                        {
                            MemorySecurity.ZeroMemory(output);
                            return false;
                        }

                        literalLength += extraLength;
                    }

                    if (literalLength > 0)
                    {
                        if (inputPtr + literalLength > inputEnd || outputPtr + literalLength > outputEnd)
                        {
                            MemorySecurity.ZeroMemory(output);
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

                    if (inputPtr + sizeof(ushort) > inputEnd)
                    {
                        MemorySecurity.ZeroMemory(output);
                        return false;
                    }

                    int offset = MemOps.ReadUnaligned<ushort>(inputPtr);
                    inputPtr += sizeof(ushort);
                    if (offset == 0 || offset > (outputPtr - outputBase))
                    {
                        MemorySecurity.ZeroMemory(output);
                        return false;
                    }

                    int matchLength = token & LZ4CompressionConstants.TokenMatchMask;
                    if (matchLength == LZ4CompressionConstants.TokenMatchMask)
                    {
                        int bytesRead = SpanOps.ReadVarInt(ref inputPtr, inputEnd, out int extraLength);
                        if (bytesRead == -1 || extraLength < 0)
                        {
                            MemorySecurity.ZeroMemory(output);
                            return false;
                        }

                        matchLength += extraLength;
                    }
                    matchLength += LZ4CompressionConstants.MinMatchLength;

                    byte* matchSourcePtr = outputPtr - offset;
                    if (outputPtr + matchLength > outputEnd)
                    {
                        MemorySecurity.ZeroMemory(output);
                        return false;
                    }

                    MemOps.Copy(matchSourcePtr, outputPtr, matchLength);
                    outputPtr += matchLength;
                }

                if (outputPtr != outputEnd || inputPtr != inputEnd)
                {
                    MemorySecurity.ZeroMemory(output);
                    return false;
                }

                bytesWritten = header.OriginalLength;
                return true;
            }
        }
    }

    #endregion Private Methods
}
