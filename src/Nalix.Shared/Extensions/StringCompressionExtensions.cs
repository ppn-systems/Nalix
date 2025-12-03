// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Shared.LZ4;

namespace Nalix.Shared.Extensions;

/// <summary>
/// Provides string helpers to compress/decompress UTF-8 text using LZ4 and encode/decode as Base64.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
public static class StringCompressionExtensions
{
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
    /// This method allocates for the UTF-8 bytes, the compressed buffer, and the Base64 output string.
    /// For most application scenarios, this is sufficient and performant.
    /// </remarks>
    /// <example>
    /// <code>
    /// string packed = "hello".CompressToBase64();
    /// </code>
    /// </example>
    /// <exception cref="System.InvalidOperationException">Thrown when compression fails.</exception>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.String CompressToBase64(this System.String? @this)
    {
        if (System.String.IsNullOrEmpty(@this))
        {
            return System.String.Empty;
        }

        // Encode UTF-8 → use stackalloc if small
        System.Int32 maxUtf8Len = System.Text.Encoding.UTF8.GetMaxByteCount(@this.Length);
        System.Span<System.Byte> utf8Buffer = maxUtf8Len <= StackAllocThreshold
            ? stackalloc System.Byte[maxUtf8Len] : new System.Byte[maxUtf8Len];

        System.Int32 utf8Len = System.Text.Encoding.UTF8.GetBytes(System.MemoryExtensions.AsSpan(@this), utf8Buffer);

        // LZ4 encode
        System.Byte[] compressed = LZ4Codec.Encode(utf8Buffer[..utf8Len]);

        // Base64 encode
        return System.Convert.ToBase64String(compressed);
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
    /// Internally performs Base64 decode, LZ4 decode, then converts bytes to UTF-8 text.
    /// </remarks>
    /// <example>
    /// <code>
    /// string text = packed.DecompressFromBase64();
    /// </code>
    /// </example>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when Base64 is invalid or decompression fails.
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

        // Base64 decode avoid throw
        System.Int32 base64Len = @this.Length;
        System.Span<System.Byte> compressedBuffer = base64Len <= StackAllocThreshold
            ? stackalloc System.Byte[base64Len]
            : new System.Byte[base64Len];

        if (!System.Convert.TryFromBase64String(@this, compressedBuffer, out System.Int32 compressedLen))
        {
            throw new System.InvalidOperationException("Invalid Base64 input.");
        }

        // LZ4 decode
        if (!LZ4Codec.Decode(compressedBuffer[..compressedLen],
            out System.Byte[]? output,
            out System.Int32 written) || output is null || written <= 0)
        {
            throw new System.InvalidOperationException("LZ4 decompression failed.");
        }

        // UTF-8 decode
        return System.Text.Encoding.UTF8.GetString(output, 0, written);
    }
}
