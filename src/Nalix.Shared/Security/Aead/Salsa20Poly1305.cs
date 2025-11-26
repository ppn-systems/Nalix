// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Shared.Memory.Internal;
using Nalix.Shared.Security.Hashing;
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
    public static void Encrypt(
        System.ReadOnlySpan<System.Byte> key, System.ReadOnlySpan<System.Byte> nonce,
        System.ReadOnlySpan<System.Byte> plaintext, System.ReadOnlySpan<System.Byte> aad,
        System.Span<System.Byte> dstCiphertext, System.Span<System.Byte> tag)
    {
        ValidateKeyNonceSizes(key, nonce);

        if (dstCiphertext.Length != plaintext.Length)
        {
            ThrowHelper.OutputLenMismatch();
        }

        if (tag.Length != TagSize)
        {
            ThrowHelper.BadTagLen();
        }

        System.Span<System.Byte> polyKey = stackalloc System.Byte[32];
        try
        {
            // 1) Derive 32-byte Poly1305 one-time key from SALSA20 counter=0
            //    Obtain raw keystream by "encrypting" zero bytes.
            System.Span<System.Byte> zeros = stackalloc System.Byte[32];
            zeros.Clear();
            _ = Salsa20.Encrypt(key, nonce, counter: 0UL, zeros, polyKey); // fill polyKey

            // 2) Encrypt payload with counter=1+
            _ = Salsa20.Encrypt(key, nonce, counter: 1UL, plaintext, dstCiphertext);

            // 3) MAC transcript (AAD || pad16 || CT || pad16 || lenAAD || lenCT)
            using Poly1305 poly = new(polyKey);
            BuildTranscriptAndFinalize(poly, aad, dstCiphertext, tag);
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
    public static System.Boolean Decrypt(
        System.ReadOnlySpan<System.Byte> key, System.ReadOnlySpan<System.Byte> nonce,
        System.ReadOnlySpan<System.Byte> ciphertext, System.ReadOnlySpan<System.Byte> aad,
        System.ReadOnlySpan<System.Byte> tag, System.Span<System.Byte> dstPlaintext)
    {
        ValidateKeyNonceSizes(key, nonce);

        if (tag.Length != TagSize)
        {
            ThrowHelper.BadTagLen();
        }

        if (dstPlaintext.Length != ciphertext.Length)
        {
            ThrowHelper.OutputLenMismatch();
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
            using (Poly1305 poly = new(polyKey))
            {
                BuildTranscriptAndFinalize(poly, aad, ciphertext, computed);
            }

            // 3) Constant-time compare
            if (!BitwiseOperations.FixedTimeEquals(computed, tag))
            {
                return false;
            }

            // 4) Decrypt with counter=1+
            _ = Salsa20.Decrypt(key, nonce, counter: 1UL, ciphertext, dstPlaintext);
            return true;
        }
        finally
        {
            MemorySecurity.ZeroMemory(polyKey);
            MemorySecurity.ZeroMemory(computed);
        }
    }

    #endregion

    #region API (convenience byte[])

    /// <summary>
    /// Encrypts plaintext and returns a newly allocated buffer containing <c>ciphertext || tag</c>.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Byte[] Encrypt(System.Byte[] key, System.Byte[] nonce, System.Byte[] plaintext, System.Byte[]? aad = null)
    {
        if (key is null || (key.Length != KEY16 && key.Length != KEY32))
        {
            ThrowHelper.BadKeyLen();
        }
        if (nonce is null || nonce.Length != NONCE8)
        {
            ThrowHelper.BadNonceLen();
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
    /// Decrypts a buffer in the form <c>ciphertext || tag</c> and returns the plaintext (or throws if tag invalid).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Byte[] Decrypt(System.Byte[] key, System.Byte[] nonce, System.Byte[] cipherWithTag, System.Byte[]? aad = null)
    {
        if (key is null || (key.Length != KEY16 && key.Length != KEY32))
        {
            ThrowHelper.BadKeyLen();
        }
        if (nonce is null || nonce.Length != NONCE8)
        {
            ThrowHelper.BadNonceLen();
        }
        if (cipherWithTag is null || cipherWithTag.Length < TagSize)
        {
            ThrowHelper.CtPlusTagTooShort();
        }

        System.Int32 ctLen = cipherWithTag.Length - TagSize;
        System.Byte[] pt = new System.Byte[ctLen];

        System.Span<System.Byte> ctSpan = System.MemoryExtensions.AsSpan(cipherWithTag, 0, ctLen);
        System.Span<System.Byte> tagSpan = System.MemoryExtensions.AsSpan(cipherWithTag, ctLen, TagSize);

        System.Boolean ok = Decrypt(key, nonce, ctSpan, aad ?? System.ReadOnlySpan<System.Byte>.Empty, tagSpan, pt);
        if (!ok)
        {
            throw new System.InvalidOperationException("Authentication failed.");
        }
        return pt;
    }

    #endregion

    #region Private helpers (transcript, padding, validation, throw helper)

    /// <summary>
    /// Updates Poly1305 with AEAD transcript and writes the final tag.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void BuildTranscriptAndFinalize(
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
        WriteUInt64LEPair(lens, 0, (System.UInt64)aad.Length, (System.UInt64)ciphertext.Length);
        mac.Update(lens);

        mac.FinalizeTag(tagOut16);
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
        MemorySecurity.ZeroMemory(pad[..(16 - rem)]);
        mac.Update(pad[..(16 - rem)]);
    }

    /// <summary>Writes two little-endian UInt64 values into destination at offset.</summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static unsafe void WriteUInt64LEPair(
        System.Span<System.Byte> dest,
        System.Int32 offset, System.UInt64 a, System.UInt64 b)
    {
        if ((System.UInt32)offset > (System.UInt32)(dest.Length - 16))
        {
            throw new System.ArgumentOutOfRangeException(nameof(offset), "Need at least 16 bytes from offset.");
        }

        if (!System.BitConverter.IsLittleEndian)
        {
            a = ReverseBytes(a);
            b = ReverseBytes(b);
        }

        fixed (System.Byte* p = &dest.GetPinnableReference())
        {
            *(System.UInt64*)(p + offset) = a;
            *(System.UInt64*)(p + offset + 8) = b;
        }
    }

    /// <summary>Byte-swap a 64-bit unsigned integer.</summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.UInt64 ReverseBytes(System.UInt64 v)
    {
        v = (v & 0x00FF00FF00FF00FFUL) << 8 | (v & 0xFF00FF00FF00FF00UL) >> 8;
        v = (v & 0x0000FFFF0000FFFFUL) << 16 | (v & 0xFFFF0000FFFF0000UL) >> 16;
        v = v << 32 | v >> 32;
        return v;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void ValidateKeyNonceSizes(
        System.ReadOnlySpan<System.Byte> key,
        System.ReadOnlySpan<System.Byte> nonce)
    {
        if (key.Length is not KEY16 and not KEY32)
        {
            ThrowHelper.BadKeyLen();
        }
        if (nonce.Length != NONCE8)
        {
            ThrowHelper.BadNonceLen();
        }
    }

    /// <summary>Centralized throw helpers (names styled to match your CHACHA20_POLY1305).</summary>
    [System.Diagnostics.StackTraceHidden]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    private static class ThrowHelper
    {
        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void BadKeyLen() => throw new System.ArgumentException("Key must be 16 or 32 bytes.", "key");

        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void BadNonceLen() => throw new System.ArgumentException("Nonce must be 8 bytes.", "nonce");

        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void BadTagLen() => throw new System.ArgumentException("Tag must be 16 bytes.", "tag");

        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void OutputLenMismatch() => throw new System.ArgumentException("Output length must match input length.");

        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void CtPlusTagTooShort() => throw new System.ArgumentException("Ciphertext+Tag is too short.", "cipherWithTag");
    }

    #endregion
}
