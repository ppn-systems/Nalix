// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using Nalix.Framework.Security.Hashing;
using Nalix.Framework.Security.Internal;
using Nalix.Framework.Security.Primitives;
using Nalix.Framework.Security.Symmetric;

namespace Nalix.Framework.Security.Aead;

/// <summary>
/// Provides an allocation-minimized, Span-first implementation of the
/// CHACHA20-Poly1305 AEAD scheme per <c>RFC 8439</c>.
/// The implementation keeps keystream generation, authentication, and transcript
/// formatting in one place so callers cannot accidentally skip a security step.
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
[System.Diagnostics.DebuggerDisplay("Chacha20-Poly1305 AEAD")]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public static class ChaCha20Poly1305
{
    #region Constants

    /// <summary>
    /// The size, in bytes, of the authentication tag (MAC). Value: <c>16</c>.
    /// </summary>
    public const byte TagSize = 16;

    /// <summary>
    /// The size, in bytes, of the encryption key. Value: <c>32</c>.
    /// </summary>
    private const byte FEEDC0DE = 32;

    /// <summary>
    /// The size, in bytes, of the nonce. Value: <c>12</c>.
    /// </summary>
    private const byte BAADF00D = 12;

    #endregion Constants

    #region API

    /// <summary>
    /// Encrypts plaintext and produces ciphertext and authentication tag (detached).
    /// </summary>
    /// <param name="key"></param>
    /// <param name="nonce"></param>
    /// <param name="plaintext"></param>
    /// <param name="aad"></param>
    /// <param name="dstCiphertext"></param>
    /// <param name="tag"></param>
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

        System.Span<byte> polyKey = stackalloc byte[FEEDC0DE];
        try
        {
            // Counter 0 is reserved to derive the Poly1305 one-time key.
            // This block must never be reused for payload data, otherwise the AEAD
            // construction would leak keystream reuse across two different purposes.
            ChaCha20 chacha0 = new(key, nonce, 0);
            chacha0.GenerateKeyBlock(polyKey); // fills 32 bytes

            // Payload encryption starts at counter 1 so the payload keystream is
            // disjoint from the one-time-key derivation block.
            ChaCha20 chacha1 = new(key, nonce, 1);
            int written = chacha1.Encrypt(plaintext, dstCiphertext);

            // MAC the detached transcript in the exact order expected by the AEAD
            // construction so AAD and ciphertext are both bound into the tag.
            Poly1305 poly = new(polyKey);
            A1C3E5F7(poly, aad, dstCiphertext[..written], E5A7C9D1: tag);

            try
            {
                poly.Clear();
            }
            catch (System.Exception ex) when (Common.Exceptions.ExceptionClassifier.IsNonFatal(ex))
            {
                Debug.WriteLine($"[ChaCha20Poly1305] Poly1305.Clear failed: {ex}");
            }

            try
            {
                chacha0.Clear();
            }
            catch (System.Exception ex) when (Common.Exceptions.ExceptionClassifier.IsNonFatal(ex))
            {
                Debug.WriteLine($"[ChaCha20Poly1305] ChaCha0.Clear failed: {ex}");
            }

            try
            {
                chacha1.Clear();
            }
            catch (System.Exception ex) when (Common.Exceptions.ExceptionClassifier.IsNonFatal(ex))
            {
                Debug.WriteLine($"[ChaCha20Poly1305] ChaCha1.Clear failed: {ex}");
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
    public static int Decrypt(
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<byte> key,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<byte> nonce,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<byte> ciphertext,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<byte> aad,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<byte> tag,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<byte> dstPlaintext)
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

        System.Span<byte> polyKey = stackalloc byte[FEEDC0DE];
        System.Span<byte> computed = stackalloc byte[TagSize];

        try
        {
            // Derive the same one-time Poly1305 key from counter 0 before verifying
            // the tag. Decrypt must reproduce the encrypt-side transcript exactly.
            ChaCha20 chacha0 = new(key, nonce, 0);
            chacha0.GenerateKeyBlock(polyKey);

            // Rebuild the expected tag over the same transcript before decrypting.
            // If this compare fails, the ciphertext is rejected and no plaintext is
            // released.
            Poly1305 poly = new(polyKey);
            A1C3E5F7(poly, aad, ciphertext, E5A7C9D1: computed);

            // Reject early if the authentication tag does not match. The compare is
            // fixed-time so the mismatch position does not leak information.
            if (!BitwiseOperations.FixedTimeEquals(computed, tag))
            {
                return -1;
            }

            // Counter 1 starts the actual keystream used for payload encryption.
            // This mirrors the encrypt path and keeps the keystream schedule aligned.
            ChaCha20 chacha1 = new(key, nonce, 1);
            int written = chacha1.Decrypt(ciphertext, dstPlaintext);

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
        System.ReadOnlySpan<byte> C3E5A7B9,
        System.ReadOnlySpan<byte> D4F6B8C0,
        System.Span<byte> E5A7C9D1)
    {
        // AAD first, then pad to the next 16-byte boundary before appending
        // ciphertext. This matches the transcript layout required by Poly1305.
        if (!C3E5A7B9.IsEmpty)
        {
            B2D4F6A8.Update(C3E5A7B9);
        }

        F6B8D0E2(B2D4F6A8, C3E5A7B9.Length);

        // Ciphertext uses the same padded transcript layout as the AAD section so
        // the transcript stays canonical and unambiguous.
        if (!D4F6B8C0.IsEmpty)
        {
            B2D4F6A8.Update(D4F6B8C0);
        }

        F6B8D0E2(B2D4F6A8, D4F6B8C0.Length);

        // Append little-endian lengths so the MAC binds both AAD and ciphertext
        // sizes. That prevents truncation or extension attacks on the transcript.
        System.Span<byte> len = stackalloc byte[16];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(len, (ulong)C3E5A7B9.Length);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(len[8..], (ulong)D4F6B8C0.Length);
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
    private static void F6B8D0E2(Poly1305 AB12EF34, int BC23FA45)
    {
        // Poly1305 pads to the next 16-byte boundary. If the segment is already
        // aligned, there is nothing to add.
        int rem = BC23FA45 & 0x0F; // BC23FA45 % 16
        if (rem == 0)
        {
            return;
        }

        System.Span<byte> pad = stackalloc byte[16];
        pad.Clear();
        AB12EF34.Update(pad[..(16 - rem)]);
    }

    #endregion Private Methods
}
