// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using Nalix.Common.Abstractions;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Security;
using Nalix.Framework.LZ4;
using Nalix.Framework.LZ4.Encoders;
using Nalix.Framework.Memory.Internal;
using Nalix.Framework.Security;
using Nalix.Framework.Security.Internal;

namespace Nalix.Framework.Frames;

/// <summary>
/// Provides high-performance packet transformation utilities including encryption,
/// decryption, compression, and decompression.
/// </summary>
/// <remarks>
/// This class operates only on the payload region (<c>DATA_REGION</c>) of a packet,
/// preserving the header section unchanged.
/// <para>
/// Designed for low-level networking scenarios with zero-allocation patterns using <see cref="Span{T}"/>.
/// </para>
/// </remarks>
public static class FrameTransformer
{
    /// <summary>
    /// Offset in bytes where the payload (DATA_REGION) starts in the packet.
    /// </summary>
    public const int Offset = (int)PacketHeaderOffset.Region;

    /// <summary>
    /// Calculates the maximum ciphertext size required for encrypting a plaintext of the given size
    /// with the specified cipher suite type.
    /// </summary>
    /// <param name="type">The cipher suite type.</param>
    /// <param name="plaintextSize">Size of the plaintext input in bytes.</param>
    /// <returns>
    /// Maximum bytes required for the ciphertext buffer, i.e., encrypted envelope size.
    /// </returns>
    public static int GetMaxCiphertextSize(CipherSuiteType type, int plaintextSize)
    {
        int tagSize = EnvelopeCipher.GetTagLength(type);
        int nonceSize = EnvelopeCipher.GetNonceLength(type);

        // Total envelope size: header + nonce + ciphertext + tag (if any)
        return EnvelopeFormat.HeaderSize + nonceSize + plaintextSize + tagSize;
    }

    /// <summary>
    /// Returns the size of plaintext from an encrypted envelope (header || nonce || ciphertext [|| tag]).
    /// </summary>
    /// <param name="envelope">The input envelope.</param>
    /// <exception cref="ArgumentException"></exception>
    public static int GetPlaintextLength(ReadOnlySpan<byte> envelope)
        => !EnvelopeFormat.TryParseEnvelope(envelope[Offset..], out EnvelopeFormat.ParsedEnvelope parsed)
        ? throw new ArgumentException("Malformed envelope", nameof(envelope)) : parsed.Ciphertext.Length;

    /// <summary>
    /// Calculates the maximum compressed size for a given plaintext size using LZ4 compression.
    /// </summary>
    /// <param name="plaintextSize">Size of the plaintext input in bytes.</param>
    public static int GetMaxCompressedSize(int plaintextSize) => LZ4BlockEncoder.GetMinOutputBufferSize(plaintextSize);

    /// <inheritdoc/>
    public static int GetDecompressedLength(ReadOnlySpan<byte> src)
    {
        LZ4BlockHeader header = MemOps.ReadUnaligned<LZ4BlockHeader>(src);

        return header.OriginalLength;
    }

    /// <summary>
    /// Encrypts the payload (DATA_REGION) of a packet while preserving the header.
    /// </summary>
    /// <param name="src">The source buffer containing the original packet.</param>
    /// <param name="dest">The destination buffer to write the encrypted packet.</param>
    /// <param name="key">The encryption key.</param>
    /// <param name="suite">The cipher suite used for encryption.</param>
    /// <returns>
    /// <c>true</c> if encryption succeeds; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// The header portion of the packet is copied unchanged.
    /// Only the payload is encrypted.
    /// </remarks>
    /// <exception cref="ArgumentNullException"></exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Encrypt(
        IBufferLease src,
        IBufferLease dest,
        ReadOnlySpan<byte> key,
        CipherSuiteType suite)
    {
        if (key.IsEmpty)
        {
            throw new ArgumentNullException(nameof(key), "Encryption key cannot be null.");
        }

        // Validate buffer sizes
        if (src.Length <= Offset || dest.Capacity < Offset)
        {
            return false;
        }

        // Copy header
        src.SpanFull[..Offset].CopyTo(dest.SpanFull[..Offset]);

        Span<byte> plainData = src.Span[Offset..];
        Span<byte> outData = dest.SpanFull[Offset..];

        // Encrypt
        _ = EnvelopeCipher.Encrypt(key, plainData, outData, null, null, suite, out int encrypted);
        dest.CommitLength(Offset + encrypted);

        return true;
    }

    /// <summary>
    /// Decrypts the payload (DATA_REGION) of a packet while preserving the header.
    /// </summary>
    /// <param name="src">The source buffer containing the encrypted packet.</param>
    /// <param name="dest">The destination buffer to write the decrypted packet.</param>
    /// <param name="key">The decryption key.</param>
    /// <returns>
    /// <c>true</c> if decryption succeeds; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// The header portion is copied unchanged.
    /// Only the payload is decrypted.
    /// </remarks>
    /// <exception cref="ArgumentNullException"></exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Decrypt(
        IBufferLease src,
        IBufferLease dest,
        ReadOnlySpan<byte> key)
    {
        if (key.IsEmpty)
        {
            throw new ArgumentNullException(nameof(key), "Encryption key cannot be null.");
        }

        // Validate buffer sizes
        if (src.Length <= Offset || dest.Capacity < Offset)
        {
            return false;
        }

        // Copy header
        src.Span[..Offset].CopyTo(dest.SpanFull[..Offset]);

        Span<byte> cipherData = src.Span[Offset..];
        Span<byte> outData = dest.SpanFull[Offset..];

        // Decrypt payload
        if (!EnvelopeCipher.Decrypt(key, cipherData, outData, null, out int decrypted))
        {
            return false;
        }

        dest.CommitLength(Offset + decrypted);

        return true;
    }

    /// <summary>
    /// Compresses the payload (DATA_REGION) of a packet using LZ4 while preserving the header.
    /// </summary>
    /// <param name="src">The source buffer containing the original packet.</param>
    /// <param name="dest">The destination buffer to write the compressed packet.</param>
    /// <returns>
    /// <c>true</c> if compression succeeds; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// The destination buffer must be large enough to hold the maximum possible compressed data.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Compress(IBufferLease src, IBufferLease dest)
    {
        if (src.Length <= Offset || dest.Capacity <= Offset)
        {
            return false;
        }

        // Copy header
        src.Span[..Offset].CopyTo(dest.SpanFull[..Offset]);

        // Compress payload
        Span<byte> input = src.Span[Offset..];
        Span<byte> output = dest.SpanFull[Offset..];

        int compressed = LZ4Codec.Encode(input, output);
        if (compressed < 0)
        {
            return false;
        }

        dest.CommitLength(Offset + compressed);
        return true;
    }

    /// <summary>
    /// Decompresses the payload (DATA_REGION) of a packet using LZ4 while preserving the header.
    /// </summary>
    /// <param name="src">The source buffer containing the compressed packet.</param>
    /// <param name="dest">The destination buffer to write the decompressed packet.</param>
    /// <returns>
    /// <c>true</c> if decompression succeeds; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// The destination buffer must be large enough to hold the original uncompressed payload.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Decompress(IBufferLease src, IBufferLease dest)
    {
        if (src.Length <= Offset || dest.Capacity <= Offset)
        {
            return false;
        }

        // Copy header
        src.Span[..Offset].CopyTo(dest.SpanFull[..Offset]);

        // Decompress payload
        Span<byte> input = src.Span[Offset..];
        Span<byte> output = dest.SpanFull[Offset..];

        int decoded = LZ4Codec.Decode(input, output);

        if (decoded < 0)
        {
            return false;
        }

        dest.CommitLength(Offset + decoded);
        return true;
    }

    /// <summary>
    /// Attempts to encrypt the packet payload.
    /// </summary>
    /// <param name="src"></param>
    /// <param name="dest"></param>
    /// <param name="key"></param>
    /// <param name="suite"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryEncrypt(
        IBufferLease src,
        IBufferLease dest,
        ReadOnlySpan<byte> key,
        CipherSuiteType suite)
    {
        try
        {
            return Encrypt(src, dest, key, suite);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Attempts to decrypt the packet payload.
    /// </summary>
    /// <param name="src"></param>
    /// <param name="dest"></param>
    /// <param name="key"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryDecrypt(
        IBufferLease src,
        IBufferLease dest,
        ReadOnlySpan<byte> key)
    {
        try
        {
            return Decrypt(src, dest, key);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Attempts to compress the packet payload.
    /// </summary>
    /// <param name="src"></param>
    /// <param name="dest"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCompress(IBufferLease src, IBufferLease dest)
    {
        try
        {
            return Compress(src, dest);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Attempts to decompress the packet payload.
    /// </summary>
    /// <param name="src"></param>
    /// <param name="dest"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryDecompress(IBufferLease src, IBufferLease dest)
    {
        try
        {
            return Decompress(src, dest);
        }
        catch
        {
            return false;
        }
    }
}
