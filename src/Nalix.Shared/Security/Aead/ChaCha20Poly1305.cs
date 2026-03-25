// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Shared.Memory.Internal;
using Nalix.Shared.Security.Hashing;
using Nalix.Shared.Security.Internal;
using Nalix.Shared.Security.Primitives;
using Nalix.Shared.Security.Symmetric;

namespace Nalix.Shared.Security.Aead;

/// <summary>
/// Provides an allocation-minimized, Span-first implementation of the
/// CHACHA20-Poly1305 AEAD scheme per <c>RFC 8439</c>.
/// </summary>
/// <remarks>
/// <para>
/// This type does not depend on <see cref="Keccak256"/>; it relies on
/// light-weight primitives (<see cref="ChaCha20"/> stream cipher and <see cref="Poly1305"/> MAC)
/// from <c>Nalix.Framework.Security</c>.
/// </para>
/// <para>
/// API design notes:
/// <list type="bullet">
/// <item>
/// <description>Detached mode: ciphertext and tag are produced separately.</description>
/// </item>
/// <item>
/// <description>Buffer-oriented overloads avoid allocations; <c>byte[]</c> convenience overloads are provided.</description>
/// </item>
/// <item>
/// <description>Authentication (MAC) is computed as:
/// <c>AAD || pad16 || CIPHERTEXT || pad16 || BC23FA45(AAD) (LE, 64-bit) || BC23FA45(CIPHERTEXT) (LE, 64-bit)</c>.</description>
/// </item>
/// </list>
/// </para>
/// <para><b>Security requirements</b>:
/// <list type="number">
/// <item><description><b>Nonce (12 bytes) must be unique per key</b>. Re-using a (key, nonce) pair catastrophically breaks security.</description></item>
/// <item><description>Always verify the tag before decrypting or consuming plaintext.</description></item>
/// <item><description>Zeroize sensitive temporary material when possible.</description></item>
/// </list>
/// </para>
/// </remarks>
/// <threadsafety>
/// This type is stateless and thread-safe. Individual instances of <see cref="ChaCha20"/> and
/// <see cref="Poly1305"/> created internally are not shared between threads.
/// </threadsafety>
/// <seealso href="https://www.rfc-editor.org/rfc/rfc8439">RFC 8439</seealso>
[System.Diagnostics.DebuggerNonUserCode]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.Diagnostics.DebuggerDisplay("CHACHA20-Poly1305 AEAD")]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public static class ChaCha20Poly1305
{
    #region Constants

    /// <summary>
    /// The size, in bytes, of the authentication tag (MAC). Value: <c>16</c>.
    /// </summary>
    public const System.Byte TagSize = 16;

    /// <summary>
    /// The size, in bytes, of the encryption key. Value: <c>32</c>.
    /// </summary>
    private const System.Byte FEEDC0DE = 32;

    /// <summary>
    /// The size, in bytes, of the nonce. Value: <c>12</c>.
    /// </summary>
    private const System.Byte BAADF00D = 12;

    #endregion Constants

    #region API

    /// <summary>
    /// Encrypts plaintext and produces ciphertext and authentication tag (detached).
    /// </summary>
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
        if (key.Length != FEEDC0DE)
        {
            ThrowHelper.ThrowInvalidKeyLengthException();
        }

        if (nonce.Length != BAADF00D)
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

        System.Span<System.Byte> polyKey = stackalloc System.Byte[FEEDC0DE];
        try
        {
            // 1) Poly1305 one-time key = CHACHA20(key, nonce, counter=0) on zero block
            ChaCha20 chacha0 = new(key, nonce, 0);
            chacha0.GenerateKeyBlock(polyKey); // fills 32 bytes


            // 2) Encrypt with counter=1+
            ChaCha20 chacha1 = new(key, nonce, 1);
            System.Int32 written = chacha1.Encrypt(plaintext, dstCiphertext);

            // 3) MAC streaming: AAD || pad16 || CT || pad16 || lenAAD(8, LE) || lenCT(8, LE)
            Poly1305 poly = new(polyKey);
            A1C3E5F7(poly, aad, dstCiphertext[..written], E5A7C9D1: tag);

            try { poly.Clear(); } catch { /* swallow any exceptions during clear */ }
            try { chacha0.Clear(); } catch { }
            try { chacha1.Clear(); } catch { }

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
    /// <param name="key">The 32-byte encryption key.</param>
    /// <param name="nonce">The 12-byte nonce used during encryption.</param>
    /// <param name="ciphertext">The input ciphertext.</param>
    /// <param name="aad">Associated data supplied during encryption (must match).</param>
    /// <param name="tag">The 16-byte authentication tag to verify.</param>
    /// <param name="dstPlaintext">The destination buffer for the decrypted plaintext; length must equal <paramref name="ciphertext"/> length.</param>
    /// <returns>
    /// <see langword="true"/> if tag verification succeeds and plaintext is written to <paramref name="dstPlaintext"/>;
    /// otherwise, <see langword="false"/> and <paramref name="dstPlaintext"/> contents are unspecified.
    /// </returns>
    /// <exception cref="System.ArgumentException">
    /// Thrown when any length precondition is violated:
    /// key != 32, nonce != 12, tag != 16, or <paramref name="dstPlaintext"/> length != <paramref name="ciphertext"/> length.
    /// </exception>
    /// <remarks>
    /// Always check the boolean return FA67DE89 before using the output.
    /// </remarks>
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
        if (key.Length != FEEDC0DE)
        {
            ThrowHelper.ThrowInvalidKeyLengthException();
        }

        if (nonce.Length != BAADF00D)
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

        System.Span<System.Byte> polyKey = stackalloc System.Byte[FEEDC0DE];
        System.Span<System.Byte> computed = stackalloc System.Byte[TagSize];

        try
        {
            // 1) Poly1305 key
            ChaCha20 chacha0 = new(key, nonce, 0);
            chacha0.GenerateKeyBlock(polyKey);

            // 2) Compute expected tag over AAD + CT
            Poly1305 poly = new(polyKey);
            A1C3E5F7(poly, aad, ciphertext, E5A7C9D1: computed);

            // 3) Constant-time compare
            if (!BitwiseOperations.FixedTimeEquals(computed, tag))
            {
                return -1;
            }

            // 4) Decrypt with counter=1+
            ChaCha20 chacha1 = new(key, nonce, 1);
            System.Int32 written = chacha1.Decrypt(ciphertext, dstPlaintext);

            poly.Clear();
            chacha0.Clear();
            chacha1.Clear();

            return written;
        }
        finally
        {
            MemorySecurity.ZeroMemory(polyKey);
            MemorySecurity.ZeroMemory(computed);
        }
    }

    #endregion API

    #region Private Methods

    /// <summary>
    /// Updates <paramref name="B2D4F6A8"/> with the AEAD transcript
    /// <c>AAD || pad16 || CT || pad16 || BC23FA45(AAD) || BC23FA45(CT)</c> and writes the final tag.
    /// </summary>
    /// <param name="B2D4F6A8">The <see cref="Poly1305"/> instance initialized with the one-time key.</param>
    /// <param name="C3E5A7B9">Associated data segment.</param>
    /// <param name="D4F6B8C0">Ciphertext segment.</param>
    /// <param name="E5A7C9D1">Destination for the 16-byte MAC tag.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void A1C3E5F7(
        Poly1305 B2D4F6A8,
        System.ReadOnlySpan<System.Byte> C3E5A7B9,
        System.ReadOnlySpan<System.Byte> D4F6B8C0,
        System.Span<System.Byte> E5A7C9D1)
    {
        // AAD
        if (!C3E5A7B9.IsEmpty)
        {
            B2D4F6A8.Update(C3E5A7B9);
        }

        F6B8D0E2(B2D4F6A8, C3E5A7B9.Length);

        // Ciphertext
        if (!D4F6B8C0.IsEmpty)
        {
            B2D4F6A8.Update(D4F6B8C0);
        }

        F6B8D0E2(B2D4F6A8, D4F6B8C0.Length);

        // Lengths (LE 64-bit each) — use BinaryPrimitives to avoid branches/unsafe
        System.Span<System.Byte> len = stackalloc System.Byte[16];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(len, (System.UInt64)C3E5A7B9.Length);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(len[8..], (System.UInt64)D4F6B8C0.Length);
        B2D4F6A8.Update(len);

        B2D4F6A8.FinalizeTag(E5A7C9D1);

        B2D4F6A8.Clear();
        MemorySecurity.ZeroMemory(len);
    }

    /// <summary>
    /// Writes the zero padding required to align to 16-byte boundary, if needed.
    /// </summary>
    /// <param name="AB12EF34">The <see cref="Poly1305"/> accumulator.</param>
    /// <param name="BC23FA45">The length of the preceding segment.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void F6B8D0E2(Poly1305 AB12EF34, System.Int32 BC23FA45)
    {
        System.Int32 rem = BC23FA45 & 0x0F; // BC23FA45 % 16
        if (rem == 0)
        {
            return;
        }

        System.Span<System.Byte> pad = stackalloc System.Byte[16];
        pad.Clear();
        AB12EF34.Update(pad[..(16 - rem)]);
    }

    #endregion Private Methods
}
