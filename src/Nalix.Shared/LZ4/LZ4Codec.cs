// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Shared.LZ4.Encoders;
using Nalix.Shared.LZ4.Engine;
using Nalix.Shared.Memory.Buffers;

namespace Nalix.Shared.LZ4;

/// <summary>
/// Provides high-performance methods for compressing and decompressing data using the Nalix LZ4 algorithm.
/// This class is static-like and designed for zero-allocation workflows.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
public static class LZ4Codec
{
    /// <summary>
    /// Compresses the input data into the specified output buffer.
    /// </summary>
    /// <param name="input">The input data to compress.</param>
    /// <param name="output">The output buffer to receive the compressed data.</param>
    /// <returns>The number of bytes written to the output buffer, or -1 if compression fails.</returns>
    /// <exception cref="System.InvalidOperationException"></exception>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static int Encode(
        [System.Diagnostics.CodeAnalysis.DisallowNull] System.ReadOnlySpan<byte> input,
        [System.Diagnostics.CodeAnalysis.DisallowNull] System.Span<byte> output)
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
                $"Memory access violation during LZ4 encoding. Input length: {input.Length}, Output length: {output.Length}", ex);
        }
    }

    /// <summary>
    /// Compresses the input data into a <see cref="BufferLease"/> rented from the pool.
    /// Caller is responsible for disposing the lease when done.
    /// </summary>
    /// <param name="input">The input data to compress.</param>
    /// <param name="lease">
    /// On success, a <see cref="BufferLease"/> whose <see cref="BufferLease.Span"/> contains
    /// exactly <c>bytesWritten</c> bytes of compressed data (including the LZ4 block header).
    /// Must be disposed by the caller. On failure, <c>null</c>.
    /// </param>
    /// <param name="bytesWritten">The number of compressed bytes written into the lease.</param>
    /// <returns><c>true</c> if compression succeeds; otherwise, <c>false</c>.</returns>
    /// <example>
    /// <code>
    /// if (LZ4Codec.Encode(data, out BufferLease? lease, out int written))
    /// {
    ///     using (lease)
    ///     {
    ///         Send(lease.Span); // zero-copy handoff
    ///     }
    /// }
    /// </code>
    /// </example>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static bool Encode(
        [System.Diagnostics.CodeAnalysis.DisallowNull] System.ReadOnlySpan<byte> input,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out BufferLease? lease,
        [System.Diagnostics.CodeAnalysis.NotNull] out int bytesWritten)
    {
        lease = null;
        bytesWritten = 0;

        int maxOutputSize = LZ4BlockEncoder.GetMaxLength(input.Length);
        BufferLease rentedLease = BufferLease.Rent(maxOutputSize);

        int written = Encode(input, rentedLease.SpanFull);

        if (written < 0)
        {
            rentedLease.Dispose();
            return false;
        }

        rentedLease.CommitLength(written);
        lease = rentedLease;
        bytesWritten = written;
        return true;
    }

    /// <summary>
    /// Decompresses the input compressed data into the specified output buffer.
    /// </summary>
    /// <param name="input">The compressed input data, including header information.</param>
    /// <param name="output">The output buffer to receive the decompressed data.</param>
    /// <returns>The number of bytes written to the output buffer, or -1 if decompression fails.</returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static int Decode(
        [System.Diagnostics.CodeAnalysis.DisallowNull] System.ReadOnlySpan<byte> input,
        [System.Diagnostics.CodeAnalysis.DisallowNull] System.Span<byte> output)
        => LZ4Decoder.Decode(input, output);

    /// <summary>
    /// Decompresses the compressed input into a newly allocated byte array.
    /// </summary>
    /// <param name="input">The compressed input data, including header information.</param>
    /// <param name="output">The output byte array containing the decompressed data, or null if decompression fails.</param>
    /// <param name="bytesWritten">The number of bytes actually written to the output array.</param>
    /// <returns>True if decompression succeeds; otherwise, false.</returns>
    /// <remarks>
    /// This overload allocates a new byte[] for the result.
    /// For hot paths, prefer <see cref="Decode(System.ReadOnlySpan{byte}, out BufferLease, out int)"/>
    /// which rents from the pool instead.
    /// </remarks>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static bool Decode(
        [System.Diagnostics.CodeAnalysis.DisallowNull] System.ReadOnlySpan<byte> input,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out byte[]? output,
        [System.Diagnostics.CodeAnalysis.DisallowNull] out int bytesWritten)
        => LZ4Decoder.Decode(input, out output, out bytesWritten);

    /// <summary>
    /// Decompresses the compressed input into a <see cref="BufferLease"/> rented from the pool.
    /// Caller is responsible for disposing the lease when done.
    /// </summary>
    /// <param name="input">The compressed input data, including header information.</param>
    /// <param name="lease">
    /// On success, a <see cref="BufferLease"/> whose <see cref="BufferLease.Span"/> contains
    /// exactly <c>bytesWritten</c> bytes of decompressed data.
    /// Must be disposed by the caller. On failure, <c>null</c>.
    /// </param>
    /// <param name="bytesWritten">The number of bytes written to the lease.</param>
    /// <returns><c>true</c> if decompression succeeds; otherwise, <c>false</c>.</returns>
    /// <example>
    /// <code>
    /// if (LZ4Codec.Decode(compressed, out BufferLease? lease, out int written))
    /// {
    ///     using (lease)
    ///     {
    ///         Process(lease.Span); // zero-copy
    ///     }
    /// }
    /// </code>
    /// </example>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static bool Decode(
        [System.Diagnostics.CodeAnalysis.DisallowNull] System.ReadOnlySpan<byte> input,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out BufferLease? lease,
        [System.Diagnostics.CodeAnalysis.NotNull] out int bytesWritten) => LZ4Decoder.Decode(input, out lease, out bytesWritten);
}
