// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Cryptography.Primitives;
using Nalix.Framework.Cryptography.Hashing;
using Nalix.Framework.Cryptography.Symmetric.Stream;

namespace Nalix.Framework.Cryptography.Aead;

/// <summary>
/// Provides an allocation-minimized, Span-first implementation of the
/// ChaCha20-Poly1305 AEAD scheme per <c>RFC 8439</c>.
/// </summary>
/// <remarks>
/// <para>
/// This type does not depend on <see cref="System.Security.Cryptography"/>; it relies on
/// light-weight primitives (<see cref="ChaCha20"/> stream cipher and <see cref="Poly1305"/> MAC)
/// from <c>Nalix.Cryptography</c>.
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
/// <c>AAD || pad16 || CIPHERTEXT || pad16 || len(AAD) (LE, 64-bit) || len(CIPHERTEXT) (LE, 64-bit)</c>.</description>
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
    private const System.Byte KeySize = 32;

    /// <summary>
    /// The size, in bytes, of the nonce. Value: <c>12</c>.
    /// </summary>
    private const System.Byte NonceSize = 12;

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
    public static void Encrypt(
        System.ReadOnlySpan<System.Byte> key,
        System.ReadOnlySpan<System.Byte> nonce,
        System.ReadOnlySpan<System.Byte> plaintext,
        System.ReadOnlySpan<System.Byte> aad,
        System.Span<System.Byte> dstCiphertext,
        System.Span<System.Byte> tag)
    {
        if (key.Length != KeySize)
        {
            ThrowHelpers.ThrowKeySize();
        }

        if (nonce.Length != NonceSize)
        {
            ThrowHelpers.ThrowNonceSize();
        }

        if (dstCiphertext.Length != plaintext.Length)
        {
            ThrowHelpers.ThrowSizeMismatch();
        }

        if (tag.Length != TagSize)
        {
            ThrowHelpers.ThrowTagSize();
        }

        System.Span<System.Byte> polyKey = stackalloc System.Byte[KeySize];
        try
        {
            // 1) Poly1305 one-time key = ChaCha20(key, nonce, counter=0) on zero block
            using (var chacha0 = new ChaCha20(key, nonce, 0))
            {
                chacha0.GenerateKeyBlock(polyKey); // fills 32 bytes
            }

            // 2) Encrypt with counter=1+
            using (var chacha1 = new ChaCha20(key, nonce, 1))
            {
                chacha1.Encrypt(plaintext, dstCiphertext);
            }

            // 3) MAC streaming: AAD || pad16 || CT || pad16 || lenAAD(8, LE) || lenCT(8, LE)
            using var poly = new Poly1305(polyKey);
            Poly1305UpdateAadCtAndLengths(poly, aad, dstCiphertext, tagOut: tag);
        }
        finally
        {
            polyKey.Clear(); // zero sensitive
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
    /// Always check the boolean return value before using the output.
    /// </remarks>
    public static System.Boolean Decrypt(
        System.ReadOnlySpan<System.Byte> key,
        System.ReadOnlySpan<System.Byte> nonce,
        System.ReadOnlySpan<System.Byte> ciphertext,
        System.ReadOnlySpan<System.Byte> aad,
        System.ReadOnlySpan<System.Byte> tag,
        System.Span<System.Byte> dstPlaintext)
    {
        if (key.Length != KeySize)
        {
            ThrowHelpers.ThrowKeySize();
        }

        if (nonce.Length != NonceSize)
        {
            ThrowHelpers.ThrowNonceSize();
        }

        if (tag.Length != TagSize)
        {
            ThrowHelpers.ThrowTagSize();
        }

        if (dstPlaintext.Length != ciphertext.Length)
        {
            ThrowHelpers.ThrowSizeMismatch();
        }

        System.Span<System.Byte> polyKey = stackalloc System.Byte[KeySize];
        System.Span<System.Byte> computed = stackalloc System.Byte[TagSize];

        try
        {
            // 1) Poly1305 key
            using (var chacha0 = new ChaCha20(key, nonce, 0))
            {
                chacha0.GenerateKeyBlock(polyKey);
            }

            // 2) Compute expected tag over AAD + CT
            using (var poly = new Poly1305(polyKey))
            {
                Poly1305UpdateAadCtAndLengths(poly, aad, ciphertext, tagOut: computed);
            }

            // 3) Constant-time compare
            if (!BitwiseOperations.FixedTimeEquals(computed, tag))
            {
                return false;
            }

            // 4) Decrypt with counter=1+
            using var chacha1 = new ChaCha20(key, nonce, 1);
            chacha1.Decrypt(ciphertext, dstPlaintext);

            return true;
        }
        finally
        {
            polyKey.Clear();
            computed.Clear();
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
    public static System.Byte[] Encrypt(
        System.Byte[] key, System.Byte[] nonce, System.Byte[] plaintext, System.Byte[]? aad = null)
    {
        if (key is null || key.Length != KeySize)
        {
            ThrowHelpers.ThrowKeySize();
        }

        if (nonce is null || nonce.Length != NonceSize)
        {
            ThrowHelpers.ThrowNonceSize();
        }

        var ct = new System.Byte[plaintext.Length];
        var tag = new System.Byte[TagSize];

        Encrypt(key, nonce,
                plaintext, aad ?? System.ReadOnlySpan<System.Byte>.Empty,
                ct, tag);

        var result = new System.Byte[ct.Length + TagSize];

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
    /// <param name="aad">Optional associated data; must match the value used for encryption.</param>
    /// <returns>The decrypted plaintext.</returns>
    /// <exception cref="System.ArgumentException">
    /// Thrown when <paramref name="key"/> or <paramref name="nonce"/> has an invalid length,
    /// or when <paramref name="cipherWithTag"/> is shorter than the tag size.
    /// </exception>
    /// <exception cref="System.InvalidOperationException">Thrown when authentication fails (tag mismatch).</exception>
    /// <remarks>
    /// This overload performs allocations; prefer the Span-based APIs in hot paths.
    /// </remarks>
    public static System.Byte[] Decrypt(System.Byte[] key, System.Byte[] nonce, System.Byte[] cipherWithTag, System.Byte[]? aad = null)
    {
        if (key is null || key.Length != KeySize)
        {
            ThrowHelpers.ThrowKeySize();
        }

        if (nonce is null || nonce.Length != NonceSize)
        {
            ThrowHelpers.ThrowNonceSize();
        }

        if (cipherWithTag is null || cipherWithTag.Length < TagSize)
        {
            ThrowHelpers.ThrowCipherSize();
        }

        var ctLen = cipherWithTag.Length - TagSize;
        var ct = System.MemoryExtensions.AsSpan(cipherWithTag, 0, ctLen);
        var tag = System.MemoryExtensions.AsSpan(cipherWithTag, ctLen, TagSize);

        var pt = new System.Byte[ctLen];
        var ok = Decrypt(key, nonce, ct, aad ?? System.ReadOnlySpan<System.Byte>.Empty, tag, pt);
        return !ok ? throw new System.InvalidOperationException("Authentication failed") : pt;
    }

    #endregion API

    #region Private Methods

    /// <summary>
    /// Updates <paramref name="poly"/> with the AEAD transcript
    /// <c>AAD || pad16 || CT || pad16 || len(AAD) || len(CT)</c> and writes the final tag.
    /// </summary>
    /// <param name="poly">The <see cref="Poly1305"/> instance initialized with the one-time key.</param>
    /// <param name="aad">Associated data segment.</param>
    /// <param name="ct">Ciphertext segment.</param>
    /// <param name="tagOut">Destination for the 16-byte MAC tag.</param>
    private static void Poly1305UpdateAadCtAndLengths(
        Poly1305 poly,
        System.ReadOnlySpan<System.Byte> aad,
        System.ReadOnlySpan<System.Byte> ct,
        System.Span<System.Byte> tagOut)
    {
        // AAD
        if (!aad.IsEmpty)
        {
            poly.Update(aad);
        }

        WritePad16(poly, aad.Length);

        // Ciphertext
        if (!ct.IsEmpty)
        {
            poly.Update(ct);
        }

        WritePad16(poly, ct.Length);

        // Lengths (LE 64-bit each)
        System.Span<System.Byte> len = stackalloc System.Byte[16];
        UnsafeWriteTwoUInt64LE(len, 0, (System.UInt64)aad.Length, (System.UInt64)ct.Length);
        poly.Update(len);

        poly.FinalizeTag(tagOut);
        len.Clear();
    }

    /// <summary>
    /// Writes the zero padding required to align to 16-byte boundary, if needed.
    /// </summary>
    /// <param name="poly">The <see cref="Poly1305"/> accumulator.</param>
    /// <param name="len">The length of the preceding segment.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void WritePad16(Poly1305 poly, System.Int32 len)
    {
        System.Int32 rem = len & 0x0F; // len % 16
        if (rem == 0)
        {
            return;
        }

        System.Span<System.Byte> pad = stackalloc System.Byte[16];
        pad[..(16 - rem)].Clear();
        poly.Update(pad[..(16 - rem)]);
    }

    /// <summary>
    /// Writes an unsigned 64-bit integer to a span at <paramref name="offset"/> in little-endian format.
    /// </summary>
    /// <param name="dst">Destination span.</param>
    /// <param name="offset">Byte offset; must have at least 8 bytes available.</param>
    /// <param name="value">The value to write.</param>
    /// <exception cref="System.ArgumentOutOfRangeException">When the destination does not have enough space.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static unsafe void UnsafeWriteUInt64LE(System.Span<System.Byte> dst, System.Int32 offset, System.UInt64 value)
    {
        if ((System.UInt32)offset > (System.UInt32)(dst.Length - 8))
        {
            throw new System.ArgumentOutOfRangeException(nameof(offset), "Need at least 8 bytes from offset.");
        }

        if (!System.BitConverter.IsLittleEndian)
        {
            value = ReverseBytes(value);
        }

        fixed (System.Byte* p = &dst.GetPinnableReference())
        {
            *(System.UInt64*)(p + offset) = value;
        }
    }

    /// <summary>
    /// Writes two unsigned 64-bit integers to a span at <paramref name="offset"/> in little-endian format.
    /// </summary>
    /// <param name="dst">Destination span.</param>
    /// <param name="offset">Byte offset; must have at least 16 bytes available.</param>
    /// <param name="v0">The first value to write.</param>
    /// <param name="v1">The second value to write.</param>
    /// <exception cref="System.ArgumentOutOfRangeException">When the destination does not have enough space.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static unsafe void UnsafeWriteTwoUInt64LE(
        System.Span<System.Byte> dst, System.Int32 offset, System.UInt64 v0, System.UInt64 v1)
    {
        if ((System.UInt32)offset > (System.UInt32)(dst.Length - 16))
        {
            throw new System.ArgumentOutOfRangeException(nameof(offset), "Need at least 16 bytes from offset.");
        }

        if (!System.BitConverter.IsLittleEndian)
        {
            v0 = ReverseBytes(v0);
            v1 = ReverseBytes(v1);
        }

        fixed (System.Byte* p = &dst.GetPinnableReference())
        {
            *(System.UInt64*)(p + offset) = v0;
            *(System.UInt64*)(p + offset + 8) = v1;
        }
    }

    /// <summary>
    /// Reverses the byte order of a 64-bit unsigned integer.
    /// </summary>
    /// <param name="v">Input value.</param>
    /// <returns>The value with bytes reversed.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.UInt64 ReverseBytes(System.UInt64 v)
    {
        v = (v & 0x00FF00FF00FF00FFUL) << 8 | (v & 0xFF00FF00FF00FF00UL) >> 8;
        v = (v & 0x0000FFFF0000FFFFUL) << 16 | (v & 0xFFFF0000FFFF0000UL) >> 16;
        v = v << 32 | v >> 32;
        return v;
    }

    /// <summary>
    /// Centralized throw helpers for fast-path argument validation.
    /// </summary>
    private static class ThrowHelpers
    {
        /// <summary>Throws when <c>key.Length != 32</c>.</summary>
        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void ThrowKeySize() => throw new System.ArgumentException("Key must be 32 bytes", "key");

        /// <summary>Throws when <c>nonce.Length != 12</c>.</summary>
        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void ThrowNonceSize() => throw new System.ArgumentException("Nonce must be 12 bytes", "nonce");

        /// <summary>Throws when <c>tag.Length != 16</c>.</summary>
        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void ThrowTagSize() => throw new System.ArgumentException("Tag must be 16 bytes", "tag");

        /// <summary>Throws when output buffer length does not match input length.</summary>
        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void ThrowSizeMismatch() => throw new System.ArgumentException("Output length must match input length.");

        /// <summary>Throws when the combined ciphertext+tag buffer is too short.</summary>
        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void ThrowCipherSize() => throw new System.ArgumentException("Ciphertext+Tag is too short.", "cipherWithTag");
    }

    #endregion Private Methods
}
