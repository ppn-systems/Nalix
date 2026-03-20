// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking.Packets;
using Nalix.Common.Security;
using Nalix.Common.Shared;
using Nalix.Shared.LZ4;
using Nalix.Shared.LZ4.Encoders;
using Nalix.Shared.Memory.Internal;
using Nalix.Shared.Security;
using Nalix.Shared.Security.Internal;

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
    /// <summary>
    /// Offset in bytes where the payload (DATA_REGION) starts in the packet.
    /// </summary>
    public const System.Int32 Offset = (System.Int32)PacketHeaderOffset.DATA_REGION;

    /// <summary>
    /// Calculates the maximum ciphertext size required for encrypting a plaintext of the given size
    /// with the specified cipher suite type.
    /// </summary>
    /// <param name="type">The cipher suite type.</param>
    /// <param name="plaintextSize">Size of the plaintext input in bytes.</param>
    /// <returns>
    /// Maximum bytes required for the ciphertext buffer, i.e., encrypted envelope size.
    /// </returns>
    public static System.Int32 GetMaxCiphertextSize(CipherSuiteType type, System.Int32 plaintextSize)
    {
        System.Int32 tagSize = EnvelopeCipher.GetTagLength(type);
        System.Int32 nonceSize = EnvelopeCipher.GetNonceLength(type);

        // Total envelope size: header + nonce + ciphertext + tag (if any)
        return EnvelopeFormat.HeaderSize + nonceSize + plaintextSize + tagSize;
    }

    /// <summary>
    /// Returns the size of plaintext from an encrypted envelope (header || nonce || ciphertext [|| tag]).
    /// </summary>
    /// <param name="envelope">The input envelope.</param>
    public static System.Int32 GetPlaintextLength(System.ReadOnlySpan<System.Byte> envelope)
        => !EnvelopeFormat.TryParseEnvelope(envelope[Offset..], out var parsed)
        ? throw new System.ArgumentException("Malformed envelope", nameof(envelope)) : parsed.Ciphertext.Length;

    /// <summary>
    /// Calculates the maximum compressed size for a given plaintext size using LZ4 compression.
    /// </summary>
    /// <param name="plaintextSize">Size of the plaintext input in bytes.</param>
    /// <returns></returns>
    public static System.Int32 GetMaxCompressedSize(System.Int32 plaintextSize) => LZ4BlockEncoder.GetMinOutputBufferSize(plaintextSize);

    /// <inheritdoc/>
    public static System.Int32 GetDecompressedLength(System.ReadOnlySpan<System.Byte> src)
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean Encrypt(
        IBufferLease src,
        IBufferLease dest,
        System.ReadOnlySpan<System.Byte> key,
        CipherSuiteType suite)
    {
        if (key.IsEmpty)
        {
            throw new System.ArgumentNullException(nameof(key), "Encryption key cannot be null.");
        }

        // Validate buffer sizes
        if (src.Length <= Offset || dest.Capacity < Offset)
        {
            return false;
        }

        // Copy header
        src.SpanFull[..Offset].CopyTo(dest.SpanFull[..Offset]);

        System.Span<System.Byte> plainData = src.Span[Offset..];
        System.Span<System.Byte> outData = dest.SpanFull[Offset..];

        // Encrypt
        EnvelopeCipher.Encrypt(key, plainData, outData, null, null, suite, out System.Int32 encrypted);
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean Decrypt(
        IBufferLease src,
        IBufferLease dest,
        System.ReadOnlySpan<System.Byte> key)
    {
        if (key.IsEmpty)
        {
            throw new System.ArgumentNullException(nameof(key), "Encryption key cannot be null.");
        }

        // Validate buffer sizes
        if (src.Length <= Offset || dest.Capacity < Offset)
        {
            return false;
        }

        // Copy header
        src.Span[..Offset].CopyTo(dest.SpanFull[..Offset]);

        System.Span<System.Byte> cipherData = src.Span[Offset..];
        System.Span<System.Byte> outData = dest.SpanFull[Offset..];

        // Decrypt payload
        if (!EnvelopeCipher.Decrypt(key, cipherData, outData, null, out System.Int32 decrypted))
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean Compress(IBufferLease src, IBufferLease dest)
    {

        if (src.Length <= Offset || dest.Capacity <= Offset)
        {
            return false;
        }

        // Copy header
        src.Span[..Offset].CopyTo(dest.SpanFull[..Offset]);

        // Compress payload
        System.Span<System.Byte> input = src.Span[Offset..];
        System.Span<System.Byte> output = dest.SpanFull[Offset..];

        System.Int32 compressed = LZ4Codec.Encode(input, output);
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean Decompress(IBufferLease src, IBufferLease dest)
    {
        if (src.Length <= Offset || dest.Capacity <= Offset)
        {
            return false;
        }

        // Copy header
        src.Span[..Offset].CopyTo(dest.SpanFull[..Offset]);

        // Decompress payload
        System.Span<System.Byte> input = src.Span[Offset..];
        System.Span<System.Byte> output = dest.SpanFull[Offset..];

        System.Int32 decoded = LZ4Codec.Decode(input, output);

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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean TryEncrypt(
        IBufferLease src,
        IBufferLease dest,
        System.ReadOnlySpan<System.Byte> key,
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean TryDecrypt(
        IBufferLease src,
        IBufferLease dest,
        System.ReadOnlySpan<System.Byte> key)
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean TryCompress(IBufferLease src, IBufferLease dest)
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean TryDecompress(IBufferLease src, IBufferLease dest)
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