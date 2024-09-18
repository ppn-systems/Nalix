// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Shared.Security.Hashing;
using Nalix.Shared.Security.Primitives;
using Nalix.Shared.Security.Symmetric;

namespace Nalix.Shared.Security.Aead;

/// <summary>
/// Allocation-minimized, Span-first AEAD built from XTEA in CTR mode + Poly1305 MAC.
/// See remarks: legacy cipher; prefer AES-GCM/ChaCha20-Poly1305 for new designs.
/// </summary>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerNonUserCode]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.Diagnostics.DebuggerDisplay("XTEA-Poly1305 AEAD (CTR)")]
public static class XteaPoly1305
{
    #region Constants

    /// <summary>
    /// Tag size in bytes (16 bytes = 128 bits).
    /// </summary>
    public const System.Byte TagSize = 16;

    private const System.Byte KEY16 = 16;
    private const System.Byte NONCE8 = 8;
    private const System.Byte BLOCK8 = 8;

    // Keep AEAD rounds consistent with your Xtea implementation.
    private const System.Byte XteaRounds = Xtea.DefaultRounds; // 64 (a.k.a. 32 cycles)

    #endregion

    #region API (detached, Span-first)

    /// <summary>
    /// Encrypts the specified plaintext using the provided key and nonce, and writes the ciphertext and authentication tag.
    /// </summary>
    /// <param name="key">The 16-byte encryption key.</param>
    /// <param name="nonce">The 8-byte nonce.</param>
    /// <param name="plaintext">The plaintext to encrypt.</param>
    /// <param name="aad">Additional authenticated data (AAD).</param>
    /// <param name="dstCiphertext">The destination buffer for the ciphertext. Must be the same length as <paramref name="plaintext"/>.</param>
    /// <param name="tag">The destination buffer for the authentication tag. Must be <c>TagSize</c> bytes long.</param>
    /// <exception cref="System.ArgumentException">Thrown if output lengths are invalid.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static void Encrypt(
        System.ReadOnlySpan<System.Byte> key, System.ReadOnlySpan<System.Byte> nonce,
        System.ReadOnlySpan<System.Byte> plaintext, System.ReadOnlySpan<System.Byte> aad,
        System.Span<System.Byte> dstCiphertext, System.Span<System.Byte> tag)
    {
        ValidateKeyNonce(key, nonce);

        if (dstCiphertext.Length != plaintext.Length)
        {
            ThrowHelper.OutputLenMismatch();
        }

        if (tag.Length != TagSize)
        {
            ThrowHelper.BadTagLen();
        }

        System.Span<System.Byte> otk = stackalloc System.Byte[32];
        try
        {
            FillPolyKeyCtr(key, nonce, otk);              // counters 0..3 -> 32B OTK
            CtrXor(key, nonce, 1UL, plaintext, dstCiphertext); // counter starts at 1

            using var poly = new Poly1305(otk);
            BuildTranscriptAndFinalize(poly, aad, dstCiphertext, tag);
        }
        finally
        {
            otk.Clear();
        }
    }

    /// <summary>
    /// Decrypts the specified ciphertext using the provided key and nonce, and writes the plaintext if authentication succeeds.
    /// </summary>
    /// <param name="key">The 16-byte encryption key.</param>
    /// <param name="nonce">The 8-byte nonce.</param>
    /// <param name="ciphertext">The ciphertext to decrypt.</param>
    /// <param name="aad">Additional authenticated data (AAD).</param>
    /// <param name="tag">The authentication tag to verify.</param>
    /// <param name="dstPlaintext">The destination buffer for the plaintext. Must be the same length as <paramref name="ciphertext"/>.</param>
    /// <returns><c>true</c> if authentication succeeds and decryption is successful; otherwise, <c>false</c>.</returns>
    /// <exception cref="System.ArgumentException">Thrown if input or output lengths are invalid.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Boolean Decrypt(
        System.ReadOnlySpan<System.Byte> key, System.ReadOnlySpan<System.Byte> nonce,
        System.ReadOnlySpan<System.Byte> ciphertext, System.ReadOnlySpan<System.Byte> aad,
        System.ReadOnlySpan<System.Byte> tag, System.Span<System.Byte> dstPlaintext)
    {
        ValidateKeyNonce(key, nonce);

        if (tag.Length != TagSize)
        {
            ThrowHelper.BadTagLen();
        }

        if (dstPlaintext.Length != ciphertext.Length)
        {
            ThrowHelper.OutputLenMismatch();
        }

        System.Span<System.Byte> otk = stackalloc System.Byte[32];
        System.Span<System.Byte> computed = stackalloc System.Byte[TagSize];

        try
        {
            FillPolyKeyCtr(key, nonce, otk);

            using (var poly = new Poly1305(otk))
            {
                BuildTranscriptAndFinalize(poly, aad, ciphertext, computed);
            }

            if (!BitwiseOperations.FixedTimeEquals(computed, tag))
            {
                return false;
            }

            CtrXor(key, nonce, 1UL, ciphertext, dstPlaintext);
            return true;
        }
        finally
        {
            otk.Clear();
            computed.Clear();
        }
    }

    #endregion

    #region API (convenience byte[])

    /// <summary>
    /// Encrypts the specified plaintext using the provided key and nonce, and returns the ciphertext concatenated with the authentication tag.
    /// </summary>
    /// <param name="key">The 16-byte encryption key.</param>
    /// <param name="nonce">The 8-byte nonce.</param>
    /// <param name="plaintext">The plaintext to encrypt.</param>
    /// <param name="aad">Optional additional authenticated data (AAD).</param>
    /// <returns>A byte array containing the ciphertext followed by the authentication tag.</returns>
    /// <exception cref="System.ArgumentException">Thrown if key or nonce lengths are invalid.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Byte[] Encrypt(System.Byte[] key, System.Byte[] nonce, System.Byte[] plaintext, System.Byte[]? aad = null)
    {
        if (key is null || key.Length != KEY16)
        {
            ThrowHelper.BadKeyLen();
        }

        if (nonce is null || nonce.Length != NONCE8)
        {
            ThrowHelper.BadNonceLen();
        }

        var ct = new System.Byte[plaintext.Length];
        var tag = new System.Byte[TagSize];

        Encrypt(key, nonce, plaintext, aad ?? System.ReadOnlySpan<System.Byte>.Empty, ct, tag);

        var result = new System.Byte[ct.Length + TagSize];
        System.MemoryExtensions.AsSpan(ct).CopyTo(result);
        System.MemoryExtensions.AsSpan(tag).CopyTo(System.MemoryExtensions.AsSpan(result, ct.Length));
        return result;
    }

    /// <summary>
    /// Decrypts the specified ciphertext with tag using the provided key and nonce, and returns the plaintext if authentication succeeds.
    /// </summary>
    /// <param name="key">The 16-byte encryption key.</param>
    /// <param name="nonce">The 8-byte nonce.</param>
    /// <param name="cipherWithTag">The ciphertext concatenated with the authentication tag.</param>
    /// <param name="aad">Optional additional authenticated data (AAD).</param>
    /// <returns>The decrypted plaintext.</returns>
    /// <exception cref="System.ArgumentException">Thrown if key, nonce, or input lengths are invalid.</exception>
    /// <exception cref="System.InvalidOperationException">Thrown if authentication fails.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Byte[] Decrypt(System.Byte[] key, System.Byte[] nonce, System.Byte[] cipherWithTag, System.Byte[]? aad = null)
    {
        if (key is null || key.Length != KEY16)
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
        var pt = new System.Byte[ctLen];

        var ctSpan = System.MemoryExtensions.AsSpan(cipherWithTag, 0, ctLen);
        var tagSpan = System.MemoryExtensions.AsSpan(cipherWithTag, ctLen, TagSize);

        System.Boolean ok = Decrypt(key, nonce, ctSpan, aad ?? System.ReadOnlySpan<System.Byte>.Empty, tagSpan, pt);
        if (!ok)
        {
            throw new System.InvalidOperationException("Authentication failed.");
        }

        return pt;
    }

    #endregion

    #region Internals (CTR keystream, transcript, helpers)

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Int32 PadLen16(System.Int32 length) => 16 - (length & 0x0F) & 0x0F;

    /// <summary>
    /// Derive 32-byte Poly1305 OTK from counters 0..3 using Xtea.Encrypt.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void FillPolyKeyCtr(
        System.ReadOnlySpan<System.Byte> key,
        System.ReadOnlySpan<System.Byte> nonce,
        System.Span<System.Byte> oneTimeKey32)
    {
        for (System.UInt64 ctr = 0; ctr < 4; ctr++)
        {
            var block = oneTimeKey32.Slice((System.Int32)(ctr * BLOCK8), BLOCK8);
            GenKeystreamBlock(key, nonce, ctr, block);
        }
    }

    /// <summary>
    /// XOR src with CTR keystream starting at startCounter into dst.
    /// </summary>
    private static void CtrXor(
        System.ReadOnlySpan<System.Byte> key,
        System.ReadOnlySpan<System.Byte> nonce,
        System.UInt64 startCounter,
        System.ReadOnlySpan<System.Byte> src,
        System.Span<System.Byte> dst)
    {
        System.Int32 offset = 0;
        System.UInt64 ctr = startCounter;
        System.Span<System.Byte> ks = stackalloc System.Byte[BLOCK8];

        while (offset < src.Length)
        {
            GenKeystreamBlock(key, nonce, ctr, ks);
            System.Int32 take = System.Math.Min(BLOCK8, src.Length - offset);

            for (System.Int32 i = 0; i < take; i++)
            {
                dst[offset + i] = (System.Byte)(src[offset + i] ^ ks[i]);
            }

            offset += take;
            ctr++;
        }

        ks.Clear();
    }

    /// <summary>
    /// Generate one 8-byte keystream block using your Xtea.Encrypt over a single 8-byte block.
    /// Input block = LE(nonce + counter) mod 2^64.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void GenKeystreamBlock(
        System.ReadOnlySpan<System.Byte> key,
        System.ReadOnlySpan<System.Byte> nonce,
        System.UInt64 counter,
        System.Span<System.Byte> out8)
    {
        // Build input block (8 bytes) as little-endian (nonce + ctr)
        System.UInt64 iv = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(nonce);
        System.UInt64 input64 = iv + counter;

        System.Span<System.Byte> in8 = stackalloc System.Byte[BLOCK8];
        System.Span<System.Byte> tmp8 = stackalloc System.Byte[BLOCK8];

        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(in8, input64);

        // Call your bulk XTEA over exactly 1 block (no padding)
        Xtea.Encrypt(in8, key, tmp8, XteaRounds);

        // Return the encrypted 8 bytes as keystream block
        tmp8.CopyTo(out8);
        in8.Clear();
        tmp8.Clear();
    }

    private static void BuildTranscriptAndFinalize(
        Poly1305 mac,
        System.ReadOnlySpan<System.Byte> aad,
        System.ReadOnlySpan<System.Byte> ciphertext,
        System.Span<System.Byte> tagOut16)
    {
        if (!aad.IsEmpty)
        {
            mac.Update(aad);
        }

        System.Int32 padAad = PadLen16(aad.Length);
        if (padAad != 0)
        {
            System.Span<System.Byte> z = stackalloc System.Byte[16];
            mac.Update(z[..padAad]);
        }

        if (!ciphertext.IsEmpty)
        {
            mac.Update(ciphertext);
        }

        System.Int32 padCt = PadLen16(ciphertext.Length);
        if (padCt != 0)
        {
            System.Span<System.Byte> z = stackalloc System.Byte[16];
            mac.Update(z[..padCt]);
        }

        System.Span<System.Byte> lens = stackalloc System.Byte[16];
        WriteUInt64LEPair(lens, 0, (System.UInt64)aad.Length, (System.UInt64)ciphertext.Length);
        mac.Update(lens);

        mac.FinalizeTag(tagOut16);
        lens.Clear();
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static unsafe void WriteUInt64LEPair(System.Span<System.Byte> dest, System.Int32 offset, System.UInt64 a, System.UInt64 b)
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
    private static void ValidateKeyNonce(System.ReadOnlySpan<System.Byte> key, System.ReadOnlySpan<System.Byte> nonce)
    {
        if (key.Length != KEY16)
        {
            ThrowHelper.BadKeyLen();
        }

        if (nonce.Length != NONCE8)
        {
            ThrowHelper.BadNonceLen();
        }
    }

    [System.Diagnostics.StackTraceHidden]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    private static class ThrowHelper
    {
        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void BadKeyLen() => throw new System.ArgumentException("Key must be 16 bytes (XTEA).", "key");
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
