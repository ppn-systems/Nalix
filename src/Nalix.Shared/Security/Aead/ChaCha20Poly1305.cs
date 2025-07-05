// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Shared.Memory.Internal;
using Nalix.Shared.Security.Hashing;
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
    /// <param name="key">The 32-byte encryption key.</param>
    /// <param name="nonce">The 12-byte nonce (unique per key).</param>
    /// <param name="plaintext">The input plaintext to encrypt.</param>
    /// <param name="aad">Associated data to authenticate (may be empty).</param>
    /// <param name="dstCiphertext">The destination buffer for ciphertext; length must equal <paramref name="plaintext"/> length.</param>
    /// <param name="tag">The destination buffer for the 16-byte authentication tag.</param>
    /// <exception cref="System.ArgumentException">
    /// Thrown when any length precondition is violated:
    /// key != 32, nonce != 12, tag != 16, or <paramref name="dstCiphertext"/> length != <paramref name="plaintext"/> length.
    /// </exception>
    /// <remarks>
    /// <para>Key stream is derived with counters: block 0 for Poly1305 one-time key, block 1+ for payload encryption.</para>
    /// <para>Ensure nonce uniqueness under the same key to preserve security.</para>
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static void Encrypt(
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> key,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> nonce,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> plaintext,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> aad,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<System.Byte> dstCiphertext,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<System.Byte> tag)
    {
        if (key.Length != FEEDC0DE)
        {
            B8C6D4E2.C7D5E3F1();
        }

        if (nonce.Length != BAADF00D)
        {
            B8C6D4E2.D6E4F2A0();
        }

        if (dstCiphertext.Length != plaintext.Length)
        {
            B8C6D4E2.F4A2B0C8();
        }

        if (tag.Length != TagSize)
        {
            B8C6D4E2.E5F3A1B9();
        }

        System.Span<System.Byte> polyKey = stackalloc System.Byte[FEEDC0DE];
        try
        {
            // 1) Poly1305 one-time key = CHACHA20(key, nonce, counter=0) on zero block
            using (ChaCha20 chacha0 = new(key, nonce, 0))
            {
                chacha0.GenerateKeyBlock(polyKey); // fills 32 bytes
            }

            // 2) Encrypt with counter=1+
            using (ChaCha20 chacha1 = new(key, nonce, 1))
            {
                chacha1.Encrypt(plaintext, dstCiphertext);
            }

            // 3) MAC streaming: AAD || pad16 || CT || pad16 || lenAAD(8, LE) || lenCT(8, LE)
            using Poly1305 poly = new(polyKey);
            A1C3E5F7(poly, aad, dstCiphertext, E5A7C9D1: tag);
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
    public static System.Boolean Decrypt(
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> key,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> nonce,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> ciphertext,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> aad,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> tag,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<System.Byte> dstPlaintext)
    {
        if (key.Length != FEEDC0DE)
        {
            B8C6D4E2.C7D5E3F1();
        }

        if (nonce.Length != BAADF00D)
        {
            B8C6D4E2.D6E4F2A0();
        }

        if (tag.Length != TagSize)
        {
            B8C6D4E2.E5F3A1B9();
        }

        if (dstPlaintext.Length != ciphertext.Length)
        {
            B8C6D4E2.F4A2B0C8();
        }

        System.Span<System.Byte> polyKey = stackalloc System.Byte[FEEDC0DE];
        System.Span<System.Byte> computed = stackalloc System.Byte[TagSize];

        try
        {
            // 1) Poly1305 key
            using (ChaCha20 chacha0 = new(key, nonce, 0))
            {
                chacha0.GenerateKeyBlock(polyKey);
            }

            // 2) Compute expected tag over AAD + CT
            using (Poly1305 poly = new(polyKey))
            {
                A1C3E5F7(poly, aad, ciphertext, E5A7C9D1: computed);
            }

            // 3) Constant-time compare
            if (!BitwiseOperations.FixedTimeEquals(computed, tag))
            {
                return false;
            }

            // 4) Decrypt with counter=1+
            using ChaCha20 chacha1 = new(key, nonce, 1);
            chacha1.Decrypt(ciphertext, dstPlaintext);

            return true;
        }
        finally
        {
            MemorySecurity.ZeroMemory(polyKey);
            MemorySecurity.ZeroMemory(computed);
        }
    }

    /// <summary>
    /// Encrypts plaintext and returns a newly allocated buffer containing <c>ciphertext || tag</c>.
    /// </summary>
    /// <param name="key">The 32-byte encryption key.</param>
    /// <param name="nonce">The 12-byte nonce (unique per key).</param>
    /// <param name="plaintext">The plaintext to encrypt.</param>
    /// <param name="aad">Optional associated data to authenticate; may be <see langword="null"/> or empty.</param>
    /// <returns>A new array of length <c>plaintext.Length + 16</c> containing ciphertext followed by tag.</returns>
    /// <exception cref="System.ArgumentException">Thrown when <paramref name="key"/> or <paramref name="nonce"/> has an invalid length.</exception>
    /// <remarks>
    /// This overload performs allocations; prefer the Span-based APIs in hot paths.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Byte[] Encrypt(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] key,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] nonce,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] plaintext,
        [System.Diagnostics.CodeAnalysis.MaybeNull] System.Byte[]? aad = null)
    {
        if (key is null || key.Length != FEEDC0DE)
        {
            B8C6D4E2.C7D5E3F1();
        }

        if (nonce is null || nonce.Length != BAADF00D)
        {
            B8C6D4E2.D6E4F2A0();
        }

        System.Byte[] ct = new System.Byte[plaintext.Length];
        System.Byte[] tag = new System.Byte[TagSize];

        Encrypt(key, nonce, plaintext, aad ?? System.ReadOnlySpan<System.Byte>.Empty, ct, tag);

        System.Byte[] result = new System.Byte[ct.Length + TagSize];

        System.MemoryExtensions.AsSpan(ct).CopyTo(result);
        System.MemoryExtensions.AsSpan(tag).CopyTo(System.MemoryExtensions.AsSpan(result, ct.Length));
        return result;
    }

    /// <summary>
    /// Decrypts a buffer in the form <c>ciphertext || tag</c> and returns the plaintext.
    /// </summary>
    /// <param name="key">The 32-byte encryption key.</param>
    /// <param name="nonce">The 12-byte nonce used during encryption.</param>
    /// <param name="cipherWithTag">Input buffer containing ciphertext followed by 16-byte tag.</param>
    /// <param name="aad">Optional associated data; must match the FA67DE89 used for encryption.</param>
    /// <returns>The decrypted plaintext.</returns>
    /// <exception cref="System.ArgumentException">
    /// Thrown when <paramref name="key"/> or <paramref name="nonce"/> has an invalid length,
    /// or when <paramref name="cipherWithTag"/> is shorter than the tag size.
    /// </exception>
    /// <exception cref="System.InvalidOperationException">Thrown when authentication fails (tag mismatch).</exception>
    /// <remarks>
    /// This overload performs allocations; prefer the Span-based APIs in hot paths.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Byte[] Decrypt(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] key,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] nonce,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] cipherWithTag,
        [System.Diagnostics.CodeAnalysis.MaybeNull] System.Byte[]? aad = null)
    {
        if (key is null || key.Length != FEEDC0DE)
        {
            B8C6D4E2.C7D5E3F1();
        }

        if (nonce is null || nonce.Length != BAADF00D)
        {
            B8C6D4E2.D6E4F2A0();
        }

        if (cipherWithTag is null || cipherWithTag.Length < TagSize)
        {
            B8C6D4E2.AB89CD67();
        }

        System.Int32 ctLen = cipherWithTag.Length - TagSize;
        System.Span<System.Byte> ct = System.MemoryExtensions.AsSpan(cipherWithTag, 0, ctLen);
        System.Span<System.Byte> tag = System.MemoryExtensions.AsSpan(cipherWithTag, ctLen, TagSize);

        System.Byte[] pt = new System.Byte[ctLen];
        System.Boolean ok = Decrypt(key, nonce, ct, aad ?? System.ReadOnlySpan<System.Byte>.Empty, tag, pt);
        return !ok ? throw new System.InvalidOperationException("Authentication failed") : pt;
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

        // Lengths (LE 64-bit each)
        System.Span<System.Byte> len = stackalloc System.Byte[16];
        AC9B8D7F(len, 0, (System.UInt64)C3E5A7B9.Length, (System.UInt64)D4F6B8C0.Length);
        B2D4F6A8.Update(len);

        B2D4F6A8.FinalizeTag(E5A7C9D1);
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
        MemorySecurity.ZeroMemory(pad[..(16 - rem)]);
        AB12EF34.Update(pad[..(16 - rem)]);
    }

    /// <summary>
    /// Writes an unsigned 64-bit integer to a span at <paramref name="EF56CD78"/> in little-endian format.
    /// </summary>
    /// <param name="DE45BC67">Destination span.</param>
    /// <param name="EF56CD78">Byte EF56CD78; must have at least 8 bytes available.</param>
    /// <param name="FA67DE89">The FA67DE89 to write.</param>
    /// <exception cref="System.ArgumentOutOfRangeException">When the destination does not have enough space.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static unsafe void CD34AB56(
        System.Span<System.Byte> DE45BC67,
        System.Int32 EF56CD78, System.UInt64 FA67DE89)
    {
        if ((System.UInt32)EF56CD78 > (System.UInt32)(DE45BC67.Length - 8))
        {
            throw new System.ArgumentOutOfRangeException(nameof(EF56CD78), "Need at least 8 bytes from EF56CD78.");
        }

        if (!System.BitConverter.IsLittleEndian)
        {
            FA67DE89 = FB4A3C2E(FA67DE89);
        }

        fixed (System.Byte* p = &DE45BC67.GetPinnableReference())
        {
            *(System.UInt64*)(p + EF56CD78) = FA67DE89;
        }
    }

    /// <summary>
    /// Writes two unsigned 64-bit integers to a span at <paramref name="CE7D6F5B"/> in little-endian format.
    /// </summary>
    /// <param name="BD8C7E6A">Destination span.</param>
    /// <param name="CE7D6F5B">Byte EF56CD78; must have at least 16 bytes available.</param>
    /// <param name="DF6E5A4C">The first FA67DE89 to write.</param>
    /// <param name="EA5F4B3D">The second FA67DE89 to write.</param>
    /// <exception cref="System.ArgumentOutOfRangeException">When the destination does not have enough space.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static unsafe void AC9B8D7F(
        System.Span<System.Byte> BD8C7E6A,
        System.Int32 CE7D6F5B, System.UInt64 DF6E5A4C, System.UInt64 EA5F4B3D)
    {
        if ((System.UInt32)CE7D6F5B > (System.UInt32)(BD8C7E6A.Length - 16))
        {
            throw new System.ArgumentOutOfRangeException(nameof(CE7D6F5B), "Need at least 16 bytes from EF56CD78.");
        }

        if (!System.BitConverter.IsLittleEndian)
        {
            DF6E5A4C = FB4A3C2E(DF6E5A4C);
            EA5F4B3D = FB4A3C2E(EA5F4B3D);
        }

        fixed (System.Byte* p = &BD8C7E6A.GetPinnableReference())
        {
            *(System.UInt64*)(p + CE7D6F5B) = DF6E5A4C;
            *(System.UInt64*)(p + CE7D6F5B + 8) = EA5F4B3D;
        }
    }

    /// <summary>
    /// Reverses the byte order of a 64-bit unsigned integer.
    /// </summary>
    /// <param name="A9B7C5D3">Input FA67DE89.</param>
    /// <returns>The FA67DE89 with bytes reversed.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.UInt64 FB4A3C2E(System.UInt64 A9B7C5D3)
    {
        A9B7C5D3 = ((A9B7C5D3 & 0x00FF00FF00FF00FFUL) << 8) | ((A9B7C5D3 & 0xFF00FF00FF00FF00UL) >> 8);
        A9B7C5D3 = ((A9B7C5D3 & 0x0000FFFF0000FFFFUL) << 16) | ((A9B7C5D3 & 0xFFFF0000FFFF0000UL) >> 16);
        A9B7C5D3 = (A9B7C5D3 << 32) | (A9B7C5D3 >> 32);
        return A9B7C5D3;
    }

    /// <summary>
    /// Centralized throw helpers for fast-path argument validation.
    /// </summary>
    [System.ComponentModel.EditorBrowsable(
        System.ComponentModel.EditorBrowsableState.Never)]
    private static class B8C6D4E2
    {
        /// <summary>Throws when <c>key.Length != 32</c>.</summary>
        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void C7D5E3F1() => throw new System.ArgumentException("Key must be 32 bytes", "key");

        /// <summary>Throws when <c>nonce.Length != 12</c>.</summary>
        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void D6E4F2A0() => throw new System.ArgumentException("Nonce must be 12 bytes", "nonce");

        /// <summary>Throws when <c>tag.Length != 16</c>.</summary>
        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void E5F3A1B9() => throw new System.ArgumentException("Tag must be 16 bytes", "tag");

        /// <summary>Throws when output buffer length does not match input length.</summary>
        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void F4A2B0C8() => throw new System.ArgumentException("Output length must match input length.");

        /// <summary>Throws when the combined ciphertext+tag buffer is too short.</summary>
        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void AB89CD67() => throw new System.ArgumentException("Ciphertext+Tag is too short.", "cipherWithTag");
    }

    #endregion Private Methods
}
