// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Shared.LZ4.Encoders;
using Nalix.Shared.LZ4.Engine;

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
    public static System.Int32 Encode(System.ReadOnlySpan<System.Byte> input, System.Span<System.Byte> output) => LZ4Encoder.Encode(input, output);

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
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] input,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] output)
        => LZ4Encoder.Encode(System.MemoryExtensions.AsSpan(input), System.MemoryExtensions.AsSpan(output));

    /// <summary>
    /// Compresses the input byte array into the specified output byte array.
    /// </summary>
    /// <param name="input">The input byte array to compress.</param>
    /// <returns>The number of bytes written to the output array.</returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Byte[] Encode(
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> input)
    {
        System.Int32 maxOutputSize = LZ4BlockEncoder.GetMaxLength(input.Length);
        System.Byte[] buffer = new System.Byte[maxOutputSize];
        System.Int32 written = Encode(input, buffer);

        return written < 0 ? throw new System.InvalidOperationException("Compression failed.") : buffer[..written];
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
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> input,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<System.Byte> output) => LZ4Decoder.Decode(input, output);

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
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] input,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] output)
        => LZ4Decoder.Decode(System.MemoryExtensions.AsSpan(input), System.MemoryExtensions.AsSpan(output));

    /// <summary>
    /// Decompresses the compressed input into a newly allocated byte array.
    /// </summary>
    /// <param name="input">The compressed input data, including header information.</param>
    /// <param name="output">The output byte array containing the decompressed data, or null if decompression fails.</param>
    /// <param name="bytesWritten">The number of bytes actually written to the output array.</param>
    /// <returns>True if decompression succeeds; otherwise, false.</returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Boolean Decode(
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> input,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out System.Byte[]? output,
        [System.Diagnostics.CodeAnalysis.NotNull] out System.Int32 bytesWritten) => LZ4Decoder.Decode(input, out output, out bytesWritten);
}
