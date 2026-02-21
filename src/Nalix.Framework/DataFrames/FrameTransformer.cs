// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Nalix.Common.Abstractions;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Security;
using Nalix.Framework.LZ4;
using Nalix.Framework.LZ4.Encoders;
using Nalix.Framework.Memory.Internal;
using Nalix.Framework.Security;
using Nalix.Framework.Security.Internal;

namespace Nalix.Framework.DataFrames;

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
    /// <exception cref="ArgumentException">Thrown when <paramref name="type"/> is not a supported cipher suite.</exception>
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
    /// <exception cref="ArgumentException">Thrown when <paramref name="envelope"/> does not contain a valid Nalix packet envelope.</exception>
    public static int GetPlaintextLength(ReadOnlySpan<byte> envelope)
    {
        EnvelopeFormat.Envelope parsed = EnvelopeFormat.ParseEnvelope(envelope[Offset..]);

        return parsed.Ciphertext.Length;
    }

    /// <summary>
    /// Calculates the maximum compressed size for a given plaintext size using LZ4 compression.
    /// </summary>
    /// <param name="plaintextSize">Size of the plaintext input in bytes.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="plaintextSize"/> is negative.</exception>
    public static int GetMaxCompressedSize(int plaintextSize) => LZ4BlockEncoder.GetMaxLength(plaintextSize);

    /// <summary>
    /// Reads the original uncompressed payload length from an LZ4 block header.
    /// </summary>
    /// <param name="src">The source packet buffer containing an LZ4-compressed payload.</param>
    /// <returns>The original payload length stored in the LZ4 block header.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="src"/> is shorter than an <see cref="LZ4BlockHeader"/>.</exception>
    public static int GetDecompressedLength(ReadOnlySpan<byte> src)
    {
        if (src.Length < Unsafe.SizeOf<LZ4BlockHeader>())
        {
            throw new ArgumentOutOfRangeException(nameof(src), "The source buffer is too small to contain an LZ4 block header.");
        }

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
    /// <remarks>
    /// The header portion of the packet is copied unchanged.
    /// Only the payload is encrypted.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="src"/> or <paramref name="dest"/> is null, or when <paramref name="key"/> is empty.</exception>
    /// <exception cref="ArgumentException">Thrown when the source or destination buffer is too small.</exception>
    /// <exception cref="CryptographicException">
    /// Thrown when the selected cipher rejects the supplied key or destination envelope.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Encrypt(IBufferLease src, IBufferLease dest, ReadOnlySpan<byte> key, CipherSuiteType suite)
    {
        ArgumentNullException.ThrowIfNull(src);
        ArgumentNullException.ThrowIfNull(dest);

        if (key.IsEmpty)
        {
            throw new ArgumentNullException(nameof(key), "Encryption key cannot be null.");
        }

        if (src.Length <= Offset)
        {
            throw new ArgumentException(
                $"Source too small: length={src.Length}, required>{Offset} (header).");
        }

        if (dest.Capacity < Offset)
        {
            throw new ArgumentException(
                $"Destination too small: capacity={dest.Capacity}, required>={Offset} (header).");
        }

        src.SpanFull[..Offset].CopyTo(dest.SpanFull[..Offset]);

        Span<byte> plainData = src.Span[Offset..];
        Span<byte> outData = dest.SpanFull[Offset..];

        EnvelopeCipher.Encrypt(key, plainData, outData, null, null, suite, out int encrypted);
        dest.CommitLength(Offset + encrypted);
    }

    /// <summary>
    /// Decrypts the payload (DATA_REGION) of a packet while preserving the header.
    /// </summary>
    /// <param name="src">The source buffer containing the encrypted packet.</param>
    /// <param name="dest">The destination buffer to write the decrypted packet.</param>
    /// <param name="key">The decryption key.</param>
    /// <remarks>
    /// The header portion is copied unchanged.
    /// Only the payload is decrypted.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="src"/> or <paramref name="dest"/> is null, or when <paramref name="key"/> is empty.</exception>
    /// <exception cref="ArgumentException">Thrown when the source or destination buffer is too small.</exception>
    /// <exception cref="CryptographicException">Thrown when AEAD authentication fails during payload decryption.</exception>
    /// <exception cref="NotSupportedException">Thrown when the encrypted payload declares an unsupported cipher suite.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Decrypt(IBufferLease src, IBufferLease dest, ReadOnlySpan<byte> key)
    {
        ArgumentNullException.ThrowIfNull(src);
        ArgumentNullException.ThrowIfNull(dest);

        if (key.IsEmpty)
        {
            throw new ArgumentNullException(nameof(key), "Encryption key cannot be null.");
        }

        if (src.Length <= Offset)
        {
            throw new ArgumentException(
                $"Source too small: length={src.Length}, required>{Offset} (header).");
        }

        if (dest.Capacity < Offset)
        {
            throw new ArgumentException(
                $"Destination too small: capacity={dest.Capacity}, required>={Offset} (header).");
        }

        src.Span[..Offset].CopyTo(dest.SpanFull[..Offset]);

        Span<byte> cipherData = src.Span[Offset..];
        Span<byte> outData = dest.SpanFull[Offset..];

        EnvelopeCipher.Decrypt(key, cipherData, outData, null, out int decrypted);

        dest.CommitLength(Offset + decrypted);
    }

    /// <summary>
    /// Compresses the payload (DATA_REGION) of a packet using LZ4 while preserving the header.
    /// </summary>
    /// <param name="src">The source buffer containing the original packet.</param>
    /// <param name="dest">The destination buffer to write the compressed packet.</param>
    /// <remarks>
    /// The destination buffer must be large enough to hold the maximum possible compressed data.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="src"/> or <paramref name="dest"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the packet header or payload buffers are too small.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Compress(IBufferLease src, IBufferLease dest)
    {
        ArgumentNullException.ThrowIfNull(src);
        ArgumentNullException.ThrowIfNull(dest);

        if (src.Length <= Offset || dest.Capacity <= Offset)
        {
            throw new ArgumentException("The source and destination buffers must contain a packet header and be large enough for the compressed payload.");
        }

        src.Span[..Offset].CopyTo(dest.SpanFull[..Offset]);

        Span<byte> input = src.Span[Offset..];
        Span<byte> output = dest.SpanFull[Offset..];

        int compressed = LZ4Codec.Encode(input, output);
        dest.CommitLength(Offset + compressed);
    }

    /// <summary>
    /// Decompresses the payload (DATA_REGION) of a packet using LZ4 while preserving the header.
    /// </summary>
    /// <param name="src">The source buffer containing the compressed packet.</param>
    /// <param name="dest">The destination buffer to write the decompressed packet.</param>
    /// <remarks>
    /// The destination buffer must be large enough to hold the original uncompressed payload.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="src"/> or <paramref name="dest"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the packet header or payload buffers are too small.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Decompress(IBufferLease src, IBufferLease dest)
    {
        ArgumentNullException.ThrowIfNull(src);
        ArgumentNullException.ThrowIfNull(dest);

        if (src.Length <= Offset || dest.Capacity <= Offset)
        {
            throw new ArgumentException("The source and destination buffers must contain a packet header and be large enough for the decompressed payload.");
        }

        src.Span[..Offset].CopyTo(dest.SpanFull[..Offset]);

        Span<byte> input = src.Span[Offset..];
        Span<byte> output = dest.SpanFull[Offset..];

        int decoded = LZ4Codec.Decode(input, output);
        dest.CommitLength(Offset + decoded);
    }
}
