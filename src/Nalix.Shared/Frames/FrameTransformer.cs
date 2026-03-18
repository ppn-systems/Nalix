// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking.Packets.Enums;
using Nalix.Common.Security.Enums;
using Nalix.Shared.LZ4;
using Nalix.Shared.Memory.Buffers;
using Nalix.Shared.Security;

namespace Nalix.Shared.Frames;

/// <summary>
/// Provides high-performance packet transformation utilities including encryption,
/// decryption, compression, and decompression.
/// </summary>
/// <remarks>
/// This class operates only on the payload region (<c>DATA_REGION</c>) of a packet,
/// preserving the header section unchanged.
/// <para>
/// Designed for low-level networking scenarios with zero-allocation patterns using <see cref="System.Span{T}"/>.
/// </para>
/// </remarks>
public static class FrameTransformer
{
    private const System.Int32 Offset = (System.Int32)PacketHeaderOffset.DATA_REGION;

    /// <summary>
    /// Encrypts the payload (DATA_REGION) of a packet while preserving the header.
    /// </summary>
    /// <param name="src">The source buffer containing the original packet.</param>
    /// <param name="dest">The destination buffer to write the encrypted packet.</param>
    /// <param name="key">The encryption key.</param>
    /// <param name="suite">The cipher suite used for encryption.</param>
    /// <param name="written">Outputs the total number of bytes written to <paramref name="dest"/>.</param>
    /// <returns>
    /// <c>true</c> if encryption succeeds; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// The header portion of the packet is copied unchanged.
    /// Only the payload is encrypted.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean Encrypt(
        BufferLease src,
        BufferLease dest,
        System.ReadOnlySpan<System.Byte> key,
        CipherSuiteType suite, out System.Int32 written)
    {
        written = 0;

        // Validate buffer sizes
        if (src.Length <= Offset || dest.Length < Offset)
        {
            return false;
        }

        // Copy header
        src.Span[..Offset].CopyTo(dest.Span[..Offset]);

        var plainData = src.Span[Offset..];
        var outData = dest.Span.Slice(Offset, plainData.Length);

        // Encrypt payload
        if (!EnvelopeCipher.Encrypt(key, plainData, outData, null, null, suite, out System.Int32 encrypted))
        {
            return false;
        }

        written = Offset + encrypted;
        dest.CommitLength(written);
        return true;
    }

    /// <summary>
    /// Decrypts the payload (DATA_REGION) of a packet while preserving the header.
    /// </summary>
    /// <param name="src">The source buffer containing the encrypted packet.</param>
    /// <param name="dest">The destination buffer to write the decrypted packet.</param>
    /// <param name="key">The decryption key.</param>
    /// <param name="written">Outputs the total number of bytes written to <paramref name="dest"/>.</param>
    /// <returns>
    /// <c>true</c> if decryption succeeds; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// The header portion is copied unchanged.
    /// Only the payload is decrypted.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean Decrypt(
        BufferLease src,
        BufferLease dest,
        System.ReadOnlySpan<System.Byte> key, out System.Int32 written)
    {
        written = 0;

        // Validate buffer sizes
        if (src.Length <= Offset || dest.Length < Offset)
        {
            return false;
        }

        // Copy header
        src.Span[..Offset].CopyTo(dest.Span[..Offset]);

        System.Span<System.Byte> cipherData = src.Span[Offset..];
        System.Span<System.Byte> outData = dest.Span.Slice(Offset, cipherData.Length);

        // Decrypt payload
        if (!EnvelopeCipher.Decrypt(key, cipherData, outData, null, out System.Int32 decrypted))
        {
            return false;
        }

        written = Offset + decrypted;
        dest.CommitLength(written);
        return true;
    }

    /// <summary>
    /// Compresses the payload (DATA_REGION) of a packet using LZ4 while preserving the header.
    /// </summary>
    /// <param name="src">The source buffer containing the original packet.</param>
    /// <param name="dest">The destination buffer to write the compressed packet.</param>
    /// <param name="written">Outputs the total number of bytes written.</param>
    /// <returns>
    /// <c>true</c> if compression succeeds; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// The destination buffer must be large enough to hold the maximum possible compressed data.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean Compress(
        BufferLease src,
        BufferLease dest, out System.Int32 written)
    {
        written = 0;

        if (src.Length <= Offset || dest.Length <= Offset)
        {
            return false;
        }

        // Copy header
        src.Span[..Offset].CopyTo(dest.Span[..Offset]);

        // Compress payload
        System.Span<System.Byte> input = src.Span[Offset..src.Length];
        System.Span<System.Byte> output = dest.Span[Offset..];

        System.Int32 compressed = LZ4Codec.Encode(input, output);
        if (compressed < 0)
        {
            return false;
        }

        written = Offset + compressed;
        dest.CommitLength(written);
        return true;
    }

    /// <summary>
    /// Decompresses the payload (DATA_REGION) of a packet using LZ4 while preserving the header.
    /// </summary>
    /// <param name="src">The source buffer containing the compressed packet.</param>
    /// <param name="dest">The destination buffer to write the decompressed packet.</param>
    /// <param name="written">Outputs the total number of bytes written.</param>
    /// <returns>
    /// <c>true</c> if decompression succeeds; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// The destination buffer must be large enough to hold the original uncompressed payload.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean Decompress(
        BufferLease src,
        BufferLease dest, out System.Int32 written)
    {
        written = 0;

        if (src.Length <= Offset || dest.Length <= Offset)
        {
            return false;
        }

        // Copy header
        src.Span[..Offset].CopyTo(dest.Span[..Offset]);

        // Decompress payload
        System.Span<System.Byte> input = src.Span[Offset..src.Length];
        System.Span<System.Byte> output = dest.Span[Offset..];

        System.Int32 decoded = LZ4Codec.Decode(input, output);
        if (decoded < 0)
        {
            return false;
        }

        written = Offset + decoded;
        dest.CommitLength(written);
        return true;
    }

    /// <summary>
    /// Attempts to encrypt the packet payload.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean TryEncrypt(
        BufferLease src,
        BufferLease dest,
        System.ReadOnlySpan<System.Byte> key,
        CipherSuiteType suite,
        out System.Int32 written)
    {
        try
        {
            return Encrypt(src, dest, key, suite, out written);
        }
        catch
        {
            written = 0;
            return false;
        }
    }

    /// <summary>
    /// Attempts to decrypt the packet payload.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean TryDecrypt(
        BufferLease src,
        BufferLease dest,
        System.ReadOnlySpan<System.Byte> key,
        out System.Int32 written)
    {
        try
        {
            return Decrypt(src, dest, key, out written);
        }
        catch
        {
            written = 0;
            return false;
        }
    }

    /// <summary>
    /// Attempts to compress the packet payload.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean TryCompress(
        BufferLease src,
        BufferLease dest,
        out System.Int32 written)
    {
        try
        {
            return Compress(src, dest, out written);
        }
        catch
        {
            written = 0;
            return false;
        }
    }

    /// <summary>
    /// Attempts to decompress the packet payload.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean TryDecompress(
        BufferLease src,
        BufferLease dest,
        out System.Int32 written)
    {
        try
        {
            return Decompress(src, dest, out written);
        }
        catch
        {
            written = 0;
            return false;
        }
    }
}