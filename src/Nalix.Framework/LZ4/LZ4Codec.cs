// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using Nalix.Common.Exceptions;
using Nalix.Framework.LZ4.Encoders;
using Nalix.Framework.LZ4.Engine;
using Nalix.Framework.Memory.Buffers;

namespace Nalix.Framework.LZ4;

/// <summary>
/// Provides high-performance LZ4 compression and decompression helpers.
/// The API is intentionally thin so callers can choose between span-based
/// zero-copy operations and pooled-buffer workflows.
/// </summary>
[DebuggerNonUserCode]
public static class LZ4Codec
{
    /// <summary>
    /// Compresses the input data into the specified output buffer.
    /// </summary>
    /// <remarks>
    /// This overload is the lowest-allocation path: the caller owns the output span
    /// and receives only the number of bytes written.
    /// </remarks>
    /// <param name="input">The input data to compress.</param>
    /// <param name="output">The output buffer to receive the compressed data.</param>
    /// <returns>The number of bytes written to the output buffer.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="output"/> is too small for the encoded block header.</exception>
    /// <exception cref="LZ4Exception">Thrown when low-level encoding hits an unexpected failure or buffer overflow.</exception>
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
            throw new InternalErrorException(
                $"LZ4 memory violation: inputLength={input.Length}, outputLength={output.Length}.", ex);
        }
    }

    /// <summary>
    /// Compresses the input data into a pooled <see cref="BufferLease"/>.
    /// </summary>
    /// <remarks>
    /// This is the convenience path for callers who want a ready-to-use pooled
    /// buffer without manually sizing and renting it first.
    /// </remarks>
    /// <param name="input">The input data to compress.</param>
    /// <param name="lease">
    /// On success, a <see cref="BufferLease"/> whose <see cref="BufferLease.Span"/> contains
    /// exactly <c>bytesWritten</c> bytes of compressed data (including the LZ4 block header).
    /// Must be disposed by the caller.
    /// </param>
    /// <param name="bytesWritten">The number of compressed bytes written into the lease.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the rented destination buffer cannot satisfy encoder requirements.</exception>
    /// <exception cref="LZ4Exception">Thrown when low-level encoding hits an unexpected failure.</exception>
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
    /// <remarks>
    /// The caller owns the destination span, which keeps this overload useful in
    /// pipelines where the final output buffer is already allocated elsewhere.
    /// </remarks>
    /// <param name="input">The compressed input data, including header information.</param>
    /// <param name="output">The output buffer to receive the decompressed data.</param>
    /// <returns>The number of bytes written to the output buffer.</returns>
    /// <exception cref="LZ4Exception">Thrown when the compressed payload is malformed or the output buffer is too small.</exception>
    [Pure]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Decode(ReadOnlySpan<byte> input, Span<byte> output) => LZ4Decoder.Decode(input, output);

    /// <summary>
    /// Decompresses the compressed input into a pooled <see cref="BufferLease"/>.
    /// </summary>
    /// <remarks>
    /// This overload is convenient when the caller does not want to size the
    /// output buffer manually and prefers to hand the result off as a lease.
    /// </remarks>
    /// <param name="input">The compressed input data, including header information.</param>
    /// <param name="lease">
    /// On success, a <see cref="BufferLease"/> whose <see cref="BufferLease.Span"/> contains
    /// exactly <c>bytesWritten</c> bytes of decompressed data.
    /// Must be disposed by the caller.
    /// </param>
    /// <param name="bytesWritten">The number of bytes written to the lease.</param>
    /// <exception cref="LZ4Exception">Thrown when the compressed payload is malformed or the decoded output cannot fit the required destination shape.</exception>
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
