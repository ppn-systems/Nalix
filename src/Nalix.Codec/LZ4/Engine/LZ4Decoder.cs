// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Nalix.Codec.Internal;
using Nalix.Codec.LZ4.Encoders;
using Nalix.Codec.Memory;
using Nalix.Codec.Security.Primitives;

namespace Nalix.Codec.LZ4.Engine;

/// <summary>
/// Provides decompression functionality for the LZ4 format.
/// </summary>
[DebuggerNonUserCode]
[EditorBrowsable(EditorBrowsableState.Never)]
internal static class LZ4Decoder
{
    #region APIs

    /// <summary>
    /// Decompresses the provided compressed data into the specified output buffer.
    /// </summary>
    /// <param name="input">The compressed data, including the header.</param>
    /// <param name="output">The buffer to store decompressed data. Size must match the original length in the header.</param>
    /// <returns>The number of bytes written to the output buffer (equal to the original length).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Decode(ReadOnlySpan<byte> input, Span<byte> output)
    {
        ReadAndValidateHeader(input, out LZ4BlockHeader header);
        return DecodeInternal(input, output, header);
    }

    /// <summary>
    /// Decompresses the provided compressed data into a <see cref="BufferLease"/> rented from the pool.
    /// Caller is responsible for disposing the lease when done.
    /// </summary>
    /// <param name="input">The compressed data, including the header.</param>
    /// <param name="lease">
    /// On success, a <see cref="BufferLease"/> whose <see cref="BufferLease.Span"/> contains
    /// exactly <c>bytesWritten</c> bytes of decompressed data. Must be disposed by the caller.
    /// This method throws on invalid compressed input or internal decode failure.
    /// </param>
    /// <param name="bytesWritten">The number of bytes written to the lease.</param>
    /// <returns><c>true</c> when decompression completes successfully.</returns>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool Decode(ReadOnlySpan<byte> input, out BufferLease? lease, out int bytesWritten)
    {
        lease = null;
        bytesWritten = 0;

        ReadAndValidateHeader(input, out LZ4BlockHeader header);

        if (header.OriginalLength == 0)
        {
            return true;
        }

        BufferLease rentedLease = BufferLease.Rent(header.OriginalLength);
        try
        {
            bytesWritten = DecodeInternal(input, rentedLease.SpanFull, header);
            rentedLease.CommitLength(bytesWritten);
            lease = rentedLease;
            return true;
        }
        catch (Exception ex) when (Common.Exceptions.ExceptionClassifier.IsNonFatal(ex))
        {
            rentedLease.Dispose();
            throw;
        }
    }

    #endregion APIs

    #region Private Methods

    /// <summary>
    /// Validates input length and reads the LZ4 block header.
    /// Centralises header checks shared by all three Decode overloads.
    /// </summary>
    /// <param name="input"></param>
    /// <param name="header"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReadAndValidateHeader(ReadOnlySpan<byte> input, out LZ4BlockHeader header)
    {
        if (input.Length < LZ4BlockHeader.Size)
        {
            throw CodecErrors.LZ4InvalidHeader;
        }

        header = MemOps.ReadUnaligned<LZ4BlockHeader>(input);

        if (header.OriginalLength < 0)
        {
            throw CodecErrors.LZ4InvalidHeader;
        }

        if (header.CompressedLength < LZ4BlockHeader.Size)
        {
            throw CodecErrors.LZ4InvalidHeader;
        }

        if (header.CompressedLength != input.Length)
        {
            throw CodecErrors.LZ4InvalidHeader;
        }

        if (header.OriginalLength > LZ4CompressionConstants.MaxBlockSize)
        {
            throw CodecErrors.LZ4InvalidHeader;
        }
    }

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static unsafe int DecodeInternal(ReadOnlySpan<byte> input, Span<byte> output, in LZ4BlockHeader header)
    {
        if (header.OriginalLength > output.Length)
        {
            throw CodecErrors.LZ4OutputBufferTooSmall;
        }

        if (header.OriginalLength == 0)
        {
            return 0;
        }

        fixed (byte* inputBase = &MemoryMarshal.GetReference(input))
        {
            fixed (byte* outputBase = &MemoryMarshal.GetReference(output))
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
                            throw CodecErrors.LZ4CorruptPayload;
                        }

                        literalLength += extraLength;
                    }

                    if (literalLength > 0)
                    {
                        if (inputPtr + literalLength > inputEnd || outputPtr + literalLength > outputEnd)
                        {
                            MemorySecurity.ZeroMemory(output);
                            throw CodecErrors.LZ4CorruptPayload;
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
                        throw CodecErrors.LZ4CorruptPayload;
                    }

                    int offset = MemOps.ReadUnaligned<ushort>(inputPtr);
                    inputPtr += sizeof(ushort);
                    if (offset == 0 || offset > (outputPtr - outputBase))
                    {
                        MemorySecurity.ZeroMemory(output);
                        throw CodecErrors.LZ4CorruptPayload;
                    }

                    int matchLength = token & LZ4CompressionConstants.TokenMatchMask;
                    if (matchLength == LZ4CompressionConstants.TokenMatchMask)
                    {
                        int bytesRead = SpanOps.ReadVarInt(ref inputPtr, inputEnd, out int extraLength);
                        if (bytesRead == -1 || extraLength < 0)
                        {
                            MemorySecurity.ZeroMemory(output);
                            throw CodecErrors.LZ4CorruptPayload;
                        }

                        matchLength += extraLength;
                    }
                    matchLength += LZ4CompressionConstants.MinMatchLength;

                    byte* matchSourcePtr = outputPtr - offset;
                    if (outputPtr + matchLength > outputEnd)
                    {
                        MemorySecurity.ZeroMemory(output);
                        throw CodecErrors.LZ4CorruptPayload;
                    }

                    MemOps.Copy(matchSourcePtr, outputPtr, matchLength);
                    outputPtr += matchLength;
                }

                if (outputPtr != outputEnd || inputPtr != inputEnd)
                {
                    MemorySecurity.ZeroMemory(output);
                    throw CodecErrors.LZ4CorruptPayload;
                }

                return header.OriginalLength;
            }
        }
    }

    #endregion Private Methods
}
