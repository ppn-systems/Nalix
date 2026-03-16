// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Shared.LZ4;
using Nalix.Shared.Memory.Buffers;

namespace Nalix.Shared.Extensions;

/// <summary>
/// Provides string helpers to compress/decompress UTF-8 text using LZ4 and encode/decode as Base64.
/// All intermediate buffers are rented from <see cref="BufferLease"/> to avoid heap allocations.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
public static class StringCompressionExtensions
{
    // Strings nhỏ hơn ngưỡng này dùng stackalloc cho UTF-8 buffer, tránh pool overhead
    private const System.Int32 StackAllocThreshold = 256;

    /// <summary>
    /// Compresses the specified text using UTF-8 + LZ4 and returns a Base64-encoded string.
    /// </summary>
    /// <param name="this">The input text to compress. If null or empty, returns <see cref="System.String.Empty"/>.</param>
    /// <returns>
    /// A Base64 string that contains the LZ4-compressed representation of <paramref name="this"/>.
    /// Returns <see cref="System.String.Empty"/> when <paramref name="this"/> is null or empty.
    /// </returns>
    /// <remarks>
    /// All intermediate buffers (UTF-8 bytes, compressed bytes) are rented from <see cref="BufferLease"/>
    /// and returned to the pool after use. The only unavoidable allocation is the final Base64 string.
    /// </remarks>
    /// <exception cref="System.InvalidOperationException">Thrown when LZ4 compression fails.</exception>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.String CompressToBase64(this System.String? @this)
    {
        if (System.String.IsNullOrEmpty(@this))
        {
            return System.String.Empty;
        }

        System.Int32 maxUtf8Len = System.Text.Encoding.UTF8.GetMaxByteCount(@this.Length);

        if (maxUtf8Len <= StackAllocThreshold)
        {
            // Path ngắn: UTF-8 trên stack, chỉ 1 BufferLease cho compressed
            System.Span<System.Byte> utf8Stack = stackalloc System.Byte[maxUtf8Len];
            System.Int32 utf8Len = System.Text.Encoding.UTF8.GetBytes(
                System.MemoryExtensions.AsSpan(@this), utf8Stack);

            return CompressSpanToBase64(utf8Stack[..utf8Len]);
        }

        // Path dài: rent buffer cho UTF-8
        using BufferLease utf8Lease = BufferLease.Rent(maxUtf8Len);

        System.Int32 utf8Written = System.Text.Encoding.UTF8.GetBytes(
            System.MemoryExtensions.AsSpan(@this), utf8Lease.SpanFull);

        utf8Lease.CommitLength(utf8Written);

        return CompressSpanToBase64(utf8Lease.Span);
    }

    /// <summary>
    /// Decodes a Base64 string that contains LZ4-compressed UTF-8 data and returns the original text.
    /// </summary>
    /// <param name="this">
    /// The Base64 string to decode. If null or empty, returns <see cref="System.String.Empty"/>.
    /// </param>
    /// <returns>
    /// The decompressed UTF-8 text represented by <paramref name="this"/>.
    /// Returns <see cref="System.String.Empty"/> when <paramref name="this"/> is null or empty.
    /// </returns>
    /// <remarks>
    /// All intermediate buffers (Base64 decoded bytes, decompressed bytes) are rented from
    /// <see cref="BufferLease"/> and returned to the pool after use.
    /// The only unavoidable allocation is the final UTF-8 string.
    /// </remarks>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when Base64 input is invalid or LZ4 decompression fails.
    /// </exception>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.String DecompressFromBase64(this System.String? @this)
    {
        if (System.String.IsNullOrEmpty(@this))
        {
            return System.String.Empty;
        }

        // Base64 decoded size luôn ≤ (base64Len / 4) * 3
        System.Int32 decodedMaxLen = @this.Length / 4 * 3;

        using BufferLease compressedLease = BufferLease.Rent(decodedMaxLen);

        if (!System.Convert.TryFromBase64String(@this, compressedLease.SpanFull, out System.Int32 compressedLen))
        {
            throw new System.InvalidOperationException("Invalid Base64 input.");
        }

        compressedLease.CommitLength(compressedLen);

        // LZ4 decode → BufferLease (pool, không alloc byte[])
        if (!LZ4Codec.Decode(compressedLease.Span, out BufferLease? decompressedLease, out System.Int32 written)
            || decompressedLease is null
            || written <= 0)
        {
            throw new System.InvalidOperationException("LZ4 decompression failed.");
        }

        using (decompressedLease)
        {
            // UTF-8 decode → string (allocation không tránh được)
            return System.Text.Encoding.UTF8.GetString(decompressedLease.Span);
        }
    }

    // ── Helper ──────────────────────────────────────────────────────────────

    /// <summary>
    /// LZ4-compresses <paramref name="utf8"/> via a pooled <see cref="BufferLease"/>,
    /// then Base64-encodes the result into a new string.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.String CompressSpanToBase64(System.ReadOnlySpan<System.Byte> utf8)
    {
        if (!LZ4Codec.Encode(utf8, out BufferLease? compressedLease, out _))
        {
            throw new System.InvalidOperationException("LZ4 compression failed.");
        }

        using (compressedLease)
        {
            // Base64 encode — string là allocation duy nhất không tránh được
            return System.Convert.ToBase64String(compressedLease!.Span);
        }
    }
}