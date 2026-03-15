// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Shared.LZ4.Encoders;
using Nalix.Shared.LZ4.Engine;

namespace Nalix.Shared.LZ4;

/// <summary>
/// Provides high-performance methods for compressing and decompressing data using the LZ4 algorithm.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
public static class LZ4Codec
{
    /// <summary>
    /// Compresses the input data into the specified output buffer.
    /// </summary>
    /// <returns>Bytes written, or -1 on failure.</returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Int32 Encode(
        System.ReadOnlySpan<System.Byte> input,
        System.Span<System.Byte> output)
    {
        if (output.Length < LZ4BlockHeader.Size)
        {
            return -1;
        }

        try
        {
            return LZ4Encoder.Encode(input, output);
        }
        catch (System.AccessViolationException ex)
        {
            throw new System.InvalidOperationException(
                $"Memory access violation during LZ4 encoding. " +
                $"Input length: {input.Length}, Output length: {output.Length}", ex);
        }
    }

    /// <summary>
    /// Compresses the input byte array into the specified output byte array.
    /// </summary>
    /// <returns>Bytes written, or -1 on failure.</returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Int32 Encode(
        System.Byte[] input,
        System.Byte[] output)
        => LZ4Encoder.Encode(
            System.MemoryExtensions.AsSpan(input),
            System.MemoryExtensions.AsSpan(output));

    /// <summary>
    /// Compresses the input data and returns a tightly-sized output array.
    /// </summary>
    /// <remarks>
    /// Rents a temporary buffer from <see cref="System.Buffers.ArrayPool{T}.Shared"/>
    /// for the compression step, then copies only the written bytes into a new
    /// exactly-sized array — one allocation instead of two (avoids <c>Array.Resize</c>).
    /// </remarks>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static System.Byte[] Encode(System.ReadOnlySpan<System.Byte> input)
    {
        System.Int32 maxOutputSize = LZ4BlockEncoder.GetMaxLength(input.Length);

        // Rent a temp buffer — returned to pool after the copy, never escapes.
        System.Byte[] temp = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(maxOutputSize);
        try
        {
            System.Int32 written = Encode(input, System.MemoryExtensions.AsSpan(temp));
            if (written < 0)
            {
                throw new System.InvalidOperationException("LZ4 compression failed.");
            }

            // One allocation: exactly the right size.
            System.Byte[] result = new System.Byte[written];
            System.MemoryExtensions.AsSpan(temp, 0, written).CopyTo(result);
            return result;
        }
        finally
        {
            System.Buffers.ArrayPool<System.Byte>.Shared.Return(temp);
        }
    }

    /// <summary>
    /// Decompresses the input compressed data into the specified output buffer.
    /// </summary>
    /// <returns>Bytes written, or -1 on failure.</returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Int32 Decode(
        System.ReadOnlySpan<System.Byte> input,
        System.Span<System.Byte> output)
        => LZ4Decoder.Decode(input, output);

    /// <summary>
    /// Decompresses the compressed input byte array into the specified output byte array.
    /// </summary>
    /// <returns>Bytes written, or -1 on failure.</returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Int32 Decode(
        System.Byte[] input,
        System.Byte[] output)
        => LZ4Decoder.Decode(
            System.MemoryExtensions.AsSpan(input),
            System.MemoryExtensions.AsSpan(output));

    /// <summary>
    /// Decompresses the compressed input into a newly allocated byte array.
    /// </summary>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static System.Boolean Decode(
        System.ReadOnlySpan<System.Byte> input,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out System.Byte[]? output,
        out System.Int32 bytesWritten)
        => LZ4Decoder.Decode(input, out output, out bytesWritten);
}