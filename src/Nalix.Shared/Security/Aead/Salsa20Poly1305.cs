// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Shared.Memory.Internal;
using Nalix.Shared.Security.Hashing;
using Nalix.Shared.Security.Internal;
using Nalix.Shared.Security.Primitives;
using Nalix.Shared.Security.Symmetric;

namespace Nalix.Shared.Security.Aead;

/// <summary>
/// Provides an allocation-minimized, Span-first implementation of a
/// SALSA20-Poly1305 AEAD scheme (secretbox-style keystream layout, AEAD transcript like RFC 8439).
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
[System.Diagnostics.DebuggerDisplay("SALSA20-Poly1305 AEAD")]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public static class Salsa20Poly1305
{
    #region Constants

    /// <summary>The size, in bytes, of the authentication tag (MAC). Value: 16.</summary>
    public const System.Byte TagSize = 16;

    /// <summary>Accepted SALSA20 key sizes in bytes.</summary>
    private const System.Byte KEY16 = 16, KEY32 = 32;

    /// <summary>The size, in bytes, of the nonce. Value: 8.</summary>
    private const System.Byte NONCE8 = 8;

    #endregion

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
    public static System.Int32 Encrypt(
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> key,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> nonce,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> plaintext,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> aad,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<System.Byte> dstCiphertext,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<System.Byte> tag)
    {
        if (key.Length is not KEY16 and not KEY32)
        {
            ThrowHelper.ThrowInvalidKeyLengthException();
        }
        if (nonce.Length != NONCE8)
        {
            ThrowHelper.ThrowInvalidNonceLengthException();
        }

        if (dstCiphertext.Length < plaintext.Length)
        {
            ThrowHelper.ThrowOutputLengthMismatchException();
        }

        if (tag.Length != TagSize)
        {
            ThrowHelper.ThrowInvalidTagLengthException();
        }

        System.Int32 written = 0;
        System.Span<System.Byte> zeros = stackalloc System.Byte[32];
        System.Span<System.Byte> polyKey = stackalloc System.Byte[32];

        try
        {
            zeros.Clear();
            // 1) Derive 32-byte Poly1305 one-time key from Salsa20 counter=0
            // Fill polyKey with keystream (encrypting zero block)
            written = Salsa20.Encrypt(key, nonce, counter: 0UL, zeros, polyKey); // typically writes 32

            // 2) Encrypt payload with counter=1+
            // If Salsa20.Encrypt returns written bytes, capture it; otherwise assume plaintext.Length.
            written = Salsa20.Encrypt(key, nonce, counter: 1UL, plaintext, dstCiphertext);

            // 3) MAC transcript (AAD || pad16 || CT || pad16 || lenAAD || lenCT)
            Poly1305 poly = new(polyKey);

            // Use only the written portion of dstCiphertext for MAC
            BUILD_TRANSCRIPT_AND_FINALIZE(poly, aad, dstCiphertext[..written], tag);

            try { poly.Clear(); } catch { } // best effort to clear any internal state if Clear throws (e.g. if already cleared)

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
    public static System.Int32 Decrypt(
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> key,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> nonce,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> ciphertext,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> aad,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> tag,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<System.Byte> dstPlaintext)
    {
        if (key.Length is not KEY16 and not KEY32)
        {
            ThrowHelper.ThrowInvalidKeyLengthException();
        }
        if (nonce.Length != NONCE8)
        {
            ThrowHelper.ThrowInvalidNonceLengthException();
        }

        if (tag.Length != TagSize)
        {
            ThrowHelper.ThrowInvalidTagLengthException();
        }

        if (dstPlaintext.Length < ciphertext.Length)
        {
            ThrowHelper.ThrowOutputLengthMismatchException();
        }

        System.Span<System.Byte> polyKey = stackalloc System.Byte[32];
        System.Span<System.Byte> computed = stackalloc System.Byte[TagSize];

        try
        {
            // 1) Poly1305 one-time key (counter=0)
            System.Span<System.Byte> zeros = stackalloc System.Byte[32];
            zeros.Clear();
            _ = Salsa20.Encrypt(key, nonce, counter: 0UL, zeros, polyKey);

            // 2) Compute expected tag over AAD + CT
            Poly1305 poly = new(polyKey);
            BUILD_TRANSCRIPT_AND_FINALIZE(poly, aad, ciphertext, computed);

            // 3) Constant-time compare
            if (!BitwiseOperations.FixedTimeEquals(computed, tag))
            {
                return -1;
            }

            // 4) Decrypt with counter=1+
            return Salsa20.Decrypt(key, nonce, counter: 1UL, ciphertext, dstPlaintext);
        }
        finally
        {
            MemorySecurity.ZeroMemory(polyKey);
            MemorySecurity.ZeroMemory(computed);
        }
    }

    #endregion

    #region Private helpers (transcript, padding, validation, throw helper)

    /// <summary>
    /// Updates Poly1305 with AEAD transcript and writes the final tag.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void BUILD_TRANSCRIPT_AND_FINALIZE(
        Poly1305 mac, System.ReadOnlySpan<System.Byte> aad,
        System.ReadOnlySpan<System.Byte> ciphertext, System.Span<System.Byte> tagOut16)
    {
        // AAD
        if (!aad.IsEmpty)
        {
            mac.Update(aad);
        }

        Pad16(mac, aad.Length);

        // Ciphertext
        if (!ciphertext.IsEmpty)
        {
            mac.Update(ciphertext);
        }

        Pad16(mac, ciphertext.Length);

        // Lengths (LE 64-bit each)
        System.Span<System.Byte> lens = stackalloc System.Byte[16];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(lens, (System.UInt64)aad.Length);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(lens[8..], (System.UInt64)ciphertext.Length);

        mac.Update(lens);
        mac.FinalizeTag(tagOut16);

        mac.Clear();
        MemorySecurity.ZeroMemory(lens);
    }

    /// <summary>Writes zero padding to align to 16-byte boundary if needed.</summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void Pad16(Poly1305 mac, System.Int32 length)
    {
        System.Int32 rem = length & 0x0F;
        if (rem == 0)
        {
            return;
        }

        System.Span<System.Byte> pad = stackalloc System.Byte[16];
        pad.Clear();

        mac.Update(pad[..(16 - rem)]);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.UInt64 ReverseBytes(System.UInt64 v)
    {
        v = ((v & 0x00FF00FF00FF00FFUL) << 8) | ((v & 0xFF00FF00FF00FF00UL) >> 8);
        v = ((v & 0x0000FFFF0000FFFFUL) << 16) | ((v & 0xFFFF0000FFFF0000UL) >> 16);
        v = (v << 32) | (v >> 32);
        return v;
    }

    #endregion
}
