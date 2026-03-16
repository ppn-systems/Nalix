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
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Int32 Encode(
        [System.Diagnostics.CodeAnalysis.DisallowNull] System.ReadOnlySpan<System.Byte> input,
        [System.Diagnostics.CodeAnalysis.DisallowNull] System.Span<System.Byte> output)
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
    /// Compresses the input byte array into the specified output byte array.
    /// </summary>
    /// <param name="input">The input byte array to compress.</param>
    /// <param name="output">The output byte array to receive the compressed data.</param>
    /// <returns>The number of bytes written to the output array, or -1 if compression fails.</returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Int32 Encode(
        [System.Diagnostics.CodeAnalysis.DisallowNull] System.Byte[] input,
        [System.Diagnostics.CodeAnalysis.DisallowNull] System.Byte[] output)
        => LZ4Encoder.Encode(System.MemoryExtensions.AsSpan(input), System.MemoryExtensions.AsSpan(output));

    /// <summary>
    /// Compresses the input data into a newly allocated byte array.
    /// </summary>
    /// <param name="input">The input data to compress.</param>
    /// <returns>A byte array containing the compressed data.</returns>
    /// <remarks>
    /// This overload allocates a new byte[] for the result.
    /// For hot paths, prefer <see cref="Encode(System.ReadOnlySpan{System.Byte}, out BufferLease, out System.Int32)"/>
    /// which rents from the pool instead.
    /// </remarks>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Byte[] Encode(
        [System.Diagnostics.CodeAnalysis.DisallowNull] System.ReadOnlySpan<System.Byte> input)
    {
        System.Int32 maxOutputSize = LZ4BlockEncoder.GetMaxLength(input.Length);
        System.Byte[] buffer = new System.Byte[maxOutputSize];

        System.Int32 written = Encode(input, buffer);

        if (written < 0)
        {
            throw new System.InvalidOperationException("Compression failed.");
        }

        System.Array.Resize(ref buffer, written);
        return buffer;
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
    public static System.Boolean Encode(
        [System.Diagnostics.CodeAnalysis.DisallowNull] System.ReadOnlySpan<System.Byte> input,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out BufferLease? lease,
        [System.Diagnostics.CodeAnalysis.NotNull] out System.Int32 bytesWritten)
    {
        lease = null;
        bytesWritten = 0;

        System.Int32 maxOutputSize = LZ4BlockEncoder.GetMaxLength(input.Length);
        BufferLease rentedLease = BufferLease.Rent(maxOutputSize);

        System.Int32 written = Encode(input, rentedLease.SpanFull);

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
    public static System.Int32 Decode(
        [System.Diagnostics.CodeAnalysis.DisallowNull] System.ReadOnlySpan<System.Byte> input,
        [System.Diagnostics.CodeAnalysis.DisallowNull] System.Span<System.Byte> output)
        => LZ4Decoder.Decode(input, output);

    /// <summary>
    /// Decompresses the compressed input byte array into the specified output byte array.
    /// </summary>
    /// <param name="input">The compressed input byte array, including header information.</param>
    /// <param name="output">The output byte array to receive the decompressed data.</param>
    /// <returns>The number of bytes written to the output array, or -1 if decompression fails.</returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Int32 Decode(
        [System.Diagnostics.CodeAnalysis.DisallowNull] System.Byte[] input,
        [System.Diagnostics.CodeAnalysis.DisallowNull] System.Byte[] output)
        => LZ4Decoder.Decode(System.MemoryExtensions.AsSpan(input), System.MemoryExtensions.AsSpan(output));

    /// <summary>
    /// Decompresses the compressed input into a newly allocated byte array.
    /// </summary>
    /// <param name="input">The compressed input data, including header information.</param>
    /// <param name="output">The output byte array containing the decompressed data, or null if decompression fails.</param>
    /// <param name="bytesWritten">The number of bytes actually written to the output array.</param>
    /// <returns>True if decompression succeeds; otherwise, false.</returns>
    /// <remarks>
    /// This overload allocates a new byte[] for the result.
    /// For hot paths, prefer <see cref="Decode(System.ReadOnlySpan{System.Byte}, out BufferLease, out System.Int32)"/>
    /// which rents from the pool instead.
    /// </remarks>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Boolean Decode(
        [System.Diagnostics.CodeAnalysis.DisallowNull] System.ReadOnlySpan<System.Byte> input,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out System.Byte[]? output,
        [System.Diagnostics.CodeAnalysis.DisallowNull] out System.Int32 bytesWritten)
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
    public static System.Boolean Decode(
        [System.Diagnostics.CodeAnalysis.DisallowNull] System.ReadOnlySpan<System.Byte> input,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out BufferLease? lease,
        [System.Diagnostics.CodeAnalysis.NotNull] out System.Int32 bytesWritten)
        => LZ4Decoder.Decode(input, out lease, out bytesWritten);
}