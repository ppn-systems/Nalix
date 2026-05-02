// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using Nalix.Codec.Internal;
using Nalix.Codec.Security.Hashing;
using Nalix.Codec.Security.Primitives;
using Nalix.Codec.Security.Symmetric;

namespace Nalix.Codec.Security.Aead;

/// <summary>
/// Provides an allocation-minimized, Span-first implementation of a
/// SALSA20-Poly1305 AEAD scheme (secretbox-style keystream layout, AEAD transcript like RFC 8439).
/// The API mirrors the ChaCha20-Poly1305 flow so callers can switch algorithms
/// without having to relearn the transcript or counter rules.
/// </summary>
/// <remarks>
/// <para>
/// Construction:
///   - Poly1305 one-time key = first 32 bytes of SALSA20 keystream with counter = 0
///   - Payload encryption = SALSA20 XOR with counter starting at 1
///   - Tag = Poly1305(AAD || pad16 || CIPHERTEXT || pad16 || len(AAD) || len(CIPHERTEXT))
/// </para>
/// <para>
/// Key size: 16 or 32 bytes (SALSA20/128 or SALSA20/256); Nonce size: 8 bytes (classic SALSA20).
/// Tag size: 16 bytes (Poly1305).
/// </para>
/// <para><b>Security requirements</b>:
/// <list type="number">
/// <item><description>Nonce (8 bytes) MUST be unique for each encryption under the same key.</description></item>
/// <item><description>Always verify tag before using decrypted plaintext.</description></item>
/// <item><description>Zeroize sensitive temporaries when possible.</description></item>
/// </list>
/// </para>
/// </remarks>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerNonUserCode]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.Diagnostics.DebuggerDisplay("Salsa20-Poly1305 AEAD")]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public static class Salsa20Poly1305
{
    #region Constants

    /// <summary>The size, in bytes, of the authentication tag (MAC). Value: 16.</summary>
    public const byte TagSize = 16;

    /// <summary>Accepted SALSA20 key sizes in bytes.</summary>
    private const byte KEY16 = 16, KEY32 = 32;

    /// <summary>The size, in bytes, of the nonce. Value: 8.</summary>
    private const byte NONCE8 = 8;

    #endregion Constants

    #region API (detached, Span-first)

    /// <summary>
    /// Encrypts plaintext and produces ciphertext and authentication tag (detached).
    /// </summary>
    /// <param name="key">16 or 32-byte key.</param>
    /// <param name="nonce">8-byte nonce (unique per key).</param>
    /// <param name="plaintext">Input plaintext.</param>
    /// <param name="aad">Associated data to authenticate (may be empty).</param>
    /// <param name="dstCiphertext">Destination for ciphertext; length must equal <paramref name="plaintext"/> length.</param>
    /// <param name="tag">Destination for the 16-byte authentication tag.</param>
    /// <exception cref="System.ArgumentException">
    /// Thrown when any length precondition is violated:
    /// key ∉ {16,32}, nonce != 8, tag != 16, or dstCiphertext length != plaintext length.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static int Encrypt(
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<byte> key,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<byte> nonce,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<byte> plaintext,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<byte> aad,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<byte> dstCiphertext,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<byte> tag)
    {
        if (key.Length is not KEY16 and not KEY32)
        {
            Throw.InvalidKeyLength();
        }
        if (nonce.Length != NONCE8)
        {
            Throw.InvalidNonceLength();
        }

        if (dstCiphertext.Length < plaintext.Length)
        {
            Throw.OutputLengthMismatch();
        }

        if (tag.Length != TagSize)
        {
            Throw.InvalidTagLength();
        }

        int written = 0;
        System.Span<byte> zeros = stackalloc byte[32];
        System.Span<byte> polyKey = stackalloc byte[32];

        try
        {
            zeros.Clear();
            // Counter 0 is reserved for the Poly1305 one-time key, so payload
            // keystream starts later and does not reuse the key-derivation block.
            written = Salsa20.Encrypt(key, nonce, counter: 0UL, zeros, polyKey); // typically writes 32

            // Counter 1 begins the payload keystream; the zero block is never reused
            // for data.
            written = Salsa20.Encrypt(key, nonce, counter: 1UL, plaintext, dstCiphertext);

            // MAC the detached transcript in the exact AEAD order so AAD and
            // ciphertext stay bound together in the final tag.
            Poly1305 poly = new(polyKey);
            // Use only the written portion of dstCiphertext for MAC
            BUILD_TRANSCRIPT_AND_FINALIZE(ref poly, aad, dstCiphertext[..written], tag);

            try
            {
                poly.Clear();
            }
            catch (System.Exception ex) when (Abstractions.Exceptions.ExceptionClassifier.IsNonFatal(ex))
            {
                Debug.WriteLine($"[Salsa20Poly1305] Poly1305.Clear failed: {ex}");
            }

            return written;
        }
        finally
        {
            MemorySecurity.ZeroMemory(polyKey);
        }
    }

    /// <summary>
    /// Decrypts ciphertext (detached) and verifies the authentication tag.
    /// </summary>
    /// <param name="key">16 or 32-byte key.</param>
    /// <param name="nonce">8-byte nonce used during encryption.</param>
    /// <param name="ciphertext">Input ciphertext.</param>
    /// <param name="aad">Associated data supplied during encryption (must match).</param>
    /// <param name="tag">The 16-byte authentication tag to verify.</param>
    /// <param name="dstPlaintext">Destination buffer for plaintext; length must equal <paramref name="ciphertext"/> length.</param>
    /// <returns>
    /// <see langword="true"/> if tag verification succeeds and plaintext is written to <paramref name="dstPlaintext"/>;
    /// otherwise <see langword="false"/>.
    /// </returns>
    /// <exception cref="System.ArgumentException">
    /// Thrown when key ∉ {16,32}, nonce != 8, tag != 16, or dstPlaintext length != ciphertext length.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static int Decrypt(
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<byte> key,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<byte> nonce,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<byte> ciphertext,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<byte> aad,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<byte> tag,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<byte> dstPlaintext)
    {
        if (key.Length is not KEY16 and not KEY32)
        {
            Throw.InvalidKeyLength();
        }
        if (nonce.Length != NONCE8)
        {
            Throw.InvalidNonceLength();
        }

        if (tag.Length != TagSize)
        {
            Throw.InvalidTagLength();
        }

        if (dstPlaintext.Length < ciphertext.Length)
        {
            Throw.OutputLengthMismatch();
        }

        System.Span<byte> polyKey = stackalloc byte[32];
        System.Span<byte> computed = stackalloc byte[TagSize];

        try
        {
            // Counter 0 again derives the Poly1305 one-time key before verifying
            // the tag. The decrypt path must reproduce the encrypt-side transcript.
            System.Span<byte> zeros = stackalloc byte[32];
            zeros.Clear();
            _ = Salsa20.Encrypt(key, nonce, counter: 0UL, zeros, polyKey);

            // Recompute the expected tag over the detached transcript before
            // decrypting. If this check fails, the ciphertext is rejected.
            Poly1305 poly = new(polyKey);
            BUILD_TRANSCRIPT_AND_FINALIZE(ref poly, aad, ciphertext, computed);

            // Reject tampered ciphertext before producing plaintext. The compare is
            // fixed-time so attackers cannot learn where the mismatch occurred.
            if (!BitwiseOperations.FixedTimeEquals(computed, tag))
            {
                return -1;
            }

            // Counter 1 begins the actual payload keystream.
            // This mirrors the encrypt path and keeps the keystream schedule aligned.
            return Salsa20.Decrypt(key, nonce, counter: 1UL, ciphertext, dstPlaintext);
        }
        finally
        {
            MemorySecurity.ZeroMemory(polyKey);
            MemorySecurity.ZeroMemory(computed);
        }
    }

    #endregion API (detached, Span-first)

    #region Private helpers (transcript, padding, validation, throw helper)

    /// <summary>
    /// Updates Poly1305 with AEAD transcript and writes the final tag.
    /// </summary>
    /// <param name="mac"></param>
    /// <param name="aad"></param>
    /// <param name="ciphertext"></param>
    /// <param name="tagOut16"></param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void BUILD_TRANSCRIPT_AND_FINALIZE(
        ref Poly1305 mac, System.ReadOnlySpan<byte> aad,
        System.ReadOnlySpan<byte> ciphertext, System.Span<byte> tagOut16)
    {
        // AAD first, then pad to the next 16-byte boundary so the transcript
        // layout matches the AEAD construction exactly.
        if (!aad.IsEmpty)
        {
            mac.Update(aad);
        }
        Pad16(ref mac, aad.Length);

        // Ciphertext follows the same padding rule as AAD so the transcript stays
        // canonical and cannot be interpreted ambiguously.
        if (!ciphertext.IsEmpty)
        {
            mac.Update(ciphertext);
        }
        Pad16(ref mac, ciphertext.Length);

        // Bind the exact lengths into the MAC so transcript truncation or
        // extension cannot be forged.
        System.Span<byte> lens = stackalloc byte[16];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(lens, (ulong)aad.Length);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(lens[8..], (ulong)ciphertext.Length);

        mac.Update(lens);
        mac.FinalizeTag(tagOut16);

        mac.Clear();
        MemorySecurity.ZeroMemory(lens);
    }

    /// <summary>Writes zero padding to align to 16-byte boundary if needed.</summary>
    /// <param name="mac"></param>
    /// <param name="length"></param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void Pad16(ref Poly1305 mac, int length)
    {
        // Poly1305 pads to the next 16-byte boundary. If the segment is already
        // aligned, there is nothing to add.
        int rem = length & 0x0F;
        if (rem == 0)
        {
            return;
        }

        System.Span<byte> pad = stackalloc byte[16];
        pad.Clear();

        mac.Update(pad[..(16 - rem)]);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static ulong ReverseBytes(ulong v)
    {
        v = ((v & 0x00FF00FF00FF00FFUL) << 8) | ((v & 0xFF00FF00FF00FF00UL) >> 8);
        v = ((v & 0x0000FFFF0000FFFFUL) << 16) | ((v & 0xFFFF0000FFFF0000UL) >> 16);
        v = (v << 32) | (v >> 32);
        return v;
    }

    #endregion Private helpers (transcript, padding, validation, throw helper)
}
