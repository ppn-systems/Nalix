// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Runtime.CompilerServices;
using Nalix.Framework.LZ4.Encoders;
using Nalix.Framework.LZ4.Engine;
using Nalix.Framework.Memory.Buffers;

namespace Nalix.Framework.LZ4;

/// <summary>
/// Provides high-performance methods for compressing and decompressing data using the Nalix LZ4 algorithm.
/// This class is static-like and designed for zero-allocation workflows.
/// </summary>
[DebuggerNonUserCode]
public static class LZ4Codec
{
    /// <summary>
    /// Compresses the input data into the specified output buffer.
    /// </summary>
    /// <param name="input">The input data to compress.</param>
    /// <param name="output">The output buffer to receive the compressed data.</param>
    /// <returns>The number of bytes written to the output buffer.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="output"/> is too small for the encoded block header.</exception>
    /// <exception cref="InvalidOperationException">Thrown when low-level encoding hits an unexpected memory access violation.</exception>
    [Pure]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Encode(ReadOnlySpan<byte> input, Span<byte> output)
    {
        try
        {
            return LZ4Encoder.Encode(input, output);
        }
        catch (AccessViolationException ex)
        {
            throw new InvalidOperationException(
                $"Memory access violation during LZ4 encoding. Input length: {input.Length}, Output length: {output.Length}", ex);
        }
    }

    /// <summary>
    /// Compresses the input data into a newly allocated byte array sized to the compressed payload.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the computed output buffer would be too small for the encoded block.</exception>
    /// <exception cref="InvalidOperationException">Thrown when low-level encoding hits an unexpected memory access violation.</exception>
    [Pure]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static byte[] Encode(ReadOnlySpan<byte> input)
    {
        byte[] buffer = new byte[LZ4BlockEncoder.GetMaxLength(input.Length)];
        int bytesWritten = Encode(input, buffer);

        if (bytesWritten == buffer.Length)
        {
            return buffer;
        }

        byte[] result = new byte[bytesWritten];
        buffer.AsSpan(0, bytesWritten).CopyTo(result);
        return result;
    }

    /// <summary>
    /// Compresses the input data into a <see cref="BufferLease"/> rented from the pool.
    /// Caller is responsible for disposing the lease when done.
    /// </summary>
    /// <param name="input">The input data to compress.</param>
    /// <param name="lease">
    /// On success, a <see cref="BufferLease"/> whose <see cref="BufferLease.Span"/> contains
    /// exactly <c>bytesWritten</c> bytes of compressed data (including the LZ4 block header).
    /// Must be disposed by the caller.
    /// </param>
    /// <param name="bytesWritten">The number of compressed bytes written into the lease.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the rented destination buffer cannot satisfy encoder requirements.</exception>
    /// <exception cref="InvalidOperationException">Thrown when low-level encoding hits an unexpected memory access violation.</exception>
    /// <example>
    /// <code>
    /// LZ4Codec.Encode(data, out BufferLease lease, out int written);
    /// using (lease)
    /// {
    ///     Send(lease.Span); // zero-copy handoff
    /// }
    /// </code>
    /// </example>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Encode(ReadOnlySpan<byte> input, out BufferLease lease, out int bytesWritten)
    {
        int maxOutputSize = LZ4BlockEncoder.GetMaxLength(input.Length);
        BufferLease rentedLease = BufferLease.Rent(maxOutputSize);
        try
        {
            int written = Encode(input, rentedLease.SpanFull);
            rentedLease.CommitLength(written);
            lease = rentedLease;
            bytesWritten = written;
        }
        catch
        {
            rentedLease.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Decompresses the input compressed data into the specified output buffer.
    /// </summary>
    /// <param name="input">The compressed input data, including header information.</param>
    /// <param name="output">The output buffer to receive the decompressed data.</param>
    /// <returns>The number of bytes written to the output buffer.</returns>
    /// <exception cref="ArgumentException">Thrown when the declared output size does not match <paramref name="output"/>.</exception>
    /// <exception cref="InvalidDataException">Thrown when the compressed payload is malformed.</exception>
    [Pure]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Decode(ReadOnlySpan<byte> input, Span<byte> output) => LZ4Decoder.Decode(input, output);

    /// <summary>
    /// Decompresses the compressed input into a newly allocated byte array.
    /// </summary>
    /// <param name="input">The compressed input data, including header information.</param>
    /// <param name="output">The output byte array containing the decompressed data.</param>
    /// <param name="bytesWritten">The number of bytes actually written to the output array.</param>
    /// <remarks>
    /// This overload allocates a new byte[] for the result.
    /// For hot paths, prefer <see cref="Decode(ReadOnlySpan{byte}, out BufferLease, out int)"/>
    /// which rents from the pool instead.
    /// </remarks>
    /// <exception cref="InvalidDataException">Thrown when the compressed payload is malformed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the underlying decoder unexpectedly returns a null buffer.</exception>
    [Pure]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Decode(ReadOnlySpan<byte> input, out byte[] output, out int bytesWritten)
    {
        _ = LZ4Decoder.Decode(input, out byte[]? decoded, out bytesWritten);
        output = decoded ?? throw new InvalidOperationException("LZ4 decoder returned a null output buffer unexpectedly.");
    }

    /// <summary>
    /// Decompresses the compressed input into a <see cref="BufferLease"/> rented from the pool.
    /// Caller is responsible for disposing the lease when done.
    /// </summary>
    /// <param name="input">The compressed input data, including header information.</param>
    /// <param name="lease">
    /// On success, a <see cref="BufferLease"/> whose <see cref="BufferLease.Span"/> contains
    /// exactly <c>bytesWritten</c> bytes of decompressed data.
    /// Must be disposed by the caller.
    /// </param>
    /// <param name="bytesWritten">The number of bytes written to the lease.</param>
    /// <exception cref="InvalidDataException">Thrown when the compressed payload is malformed.</exception>
    /// <exception cref="ArgumentException">Thrown when the decoded output cannot fit the required destination shape.</exception>
    /// <example>
    /// <code>
    /// LZ4Codec.Decode(compressed, out BufferLease lease, out int written);
    /// using (lease)
    /// {
    ///     Process(lease.Span); // zero-copy
    /// }
    /// </code>
    /// </example>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Decode(ReadOnlySpan<byte> input, out BufferLease? lease, out int bytesWritten) => _ = LZ4Decoder.Decode(input, out lease, out bytesWritten);
}
