// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Framework.Cryptography.Hashing;
using Nalix.Framework.Cryptography.Primitives;
using Nalix.Framework.Cryptography.Symmetric;

namespace Nalix.Framework.Cryptography.Aead;

/// <summary>
/// Speck-CTR + Poly1305 AEAD (detached, Span-first) for Speck 128/256.
/// <para>
/// Construction:
///   - Keystream block i = Speck_Encrypt( nonce_128 + i ) where addition is 128-bit LE (low then high 64-bit)
///   - Poly1305 one-time key (32 bytes) = keystream blocks for counters 0 and 1 (concatenated)
///   - Payload encryption = XOR with keystream starting at counter = 2
///   - Tag = Poly1305( AAD || pad16 || CIPHERTEXT || pad16 || len(AAD) || len(CT) )
/// </para>
/// <remarks>
/// Key size: 32 bytes (Speck 128/256). Nonce size: 16 bytes. Tag: 16 bytes.
/// This is a custom AEAD to align APIs across your Nalix cipher suite; for new designs prefer AES-GCM / ChaCha20-Poly1305.
/// </remarks>
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.Diagnostics.DebuggerDisplay("Speck-Poly1305 AEAD (CTR)")]
public static class SpeckPoly1305
{
    #region Constants

    /// <summary>Poly1305 tag size in bytes.</summary>
    public const System.Byte TagSize = 16;

    /// <summary>Speck 128/256 key size in bytes.</summary>
    private const System.Int32 KEY32 = Speck.KeySizeBytes; // 32

    /// <summary>Nonce size in bytes (128-bit CTR IV).</summary>
    private const System.Int32 NONCE16 = 16;

    /// <summary>Speck block size in bytes (128-bit).</summary>
    private const System.Int32 BLOCK16 = Speck.BlockSizeBytes; // 16

    #endregion

    #region API (detached, Span-first)

    /// <summary>
    /// Encrypts plaintext and produces ciphertext and authentication tag (detached).
    /// </summary>
    /// <param name="key">32-byte Speck key.</param>
    /// <param name="nonce">16-byte nonce (unique per key).</param>
    /// <param name="plaintext">Input plaintext.</param>
    /// <param name="aad">Associated data to authenticate (may be empty).</param>
    /// <param name="dstCiphertext">Destination for ciphertext; length must equal <paramref name="plaintext"/> length.</param>
    /// <param name="tag">Destination for the 16-byte authentication tag.</param>
    public static void Encrypt(
        System.ReadOnlySpan<System.Byte> key,
        System.ReadOnlySpan<System.Byte> nonce,
        System.ReadOnlySpan<System.Byte> plaintext,
        System.ReadOnlySpan<System.Byte> aad,
        System.Span<System.Byte> dstCiphertext,
        System.Span<System.Byte> tag)
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

        System.Span<System.Byte> otk = stackalloc System.Byte[32]; // Poly1305 one-time key
        try
        {
            // 1) Derive one-time Poly1305 key from counters 0 and 1 (2 * 16 = 32 bytes)
            FillPolyKeyCtr(key, nonce, otk);

            // 2) Encrypt payload with CTR starting at counter = 2
            CtrXor(key, nonce, startCounter: 2UL, plaintext, dstCiphertext);

            // 3) MAC transcript
            using var poly = new Poly1305(otk);
            BuildTranscriptAndFinalize(poly, aad, dstCiphertext, tag);
        }
        finally
        {
            otk.Clear();
        }
    }

    /// <summary>
    /// Decrypts ciphertext (detached) and verifies the authentication tag.
    /// </summary>
    /// <param name="key">32-byte Speck key.</param>
    /// <param name="nonce">16-byte nonce used during encryption.</param>
    /// <param name="ciphertext">Ciphertext to decrypt.</param>
    /// <param name="aad">Associated data supplied during encryption (must match exactly).</param>
    /// <param name="tag">16-byte authentication tag to verify.</param>
    /// <param name="dstPlaintext">Destination for plaintext; length must equal <paramref name="ciphertext"/> length.</param>
    /// <returns><c>true</c> if tag verification succeeds and plaintext is written; otherwise <c>false</c>.</returns>
    public static System.Boolean Decrypt(
        System.ReadOnlySpan<System.Byte> key,
        System.ReadOnlySpan<System.Byte> nonce,
        System.ReadOnlySpan<System.Byte> ciphertext,
        System.ReadOnlySpan<System.Byte> aad,
        System.ReadOnlySpan<System.Byte> tag,
        System.Span<System.Byte> dstPlaintext)
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
            // 1) OTK from counters 0 and 1
            FillPolyKeyCtr(key, nonce, otk);

            // 2) Compute expected tag
            using (var poly = new Poly1305(otk))
            {
                BuildTranscriptAndFinalize(poly, aad, ciphertext, computed);
            }

            // 3) Constant-time compare
            if (!BitwiseOperations.FixedTimeEquals(computed, tag))
            {
                return false;
            }

            // 4) Decrypt with CTR starting at counter = 2
            CtrXor(key, nonce, startCounter: 2UL, ciphertext, dstPlaintext);
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
    /// Encrypts plaintext and returns a newly allocated buffer containing <c>ciphertext || tag</c>.
    /// </summary>
    public static System.Byte[] Encrypt(System.Byte[] key, System.Byte[] nonce, System.Byte[] plaintext, System.Byte[]? aad = null)
    {
        if (key is null || key.Length != KEY32)
        {
            ThrowHelper.BadKeyLen();
        }

        if (nonce is null || nonce.Length != NONCE16)
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
    /// Decrypts a buffer in the form <c>ciphertext || tag</c> and returns the plaintext (or throws if tag invalid).
    /// </summary>
    public static System.Byte[] Decrypt(System.Byte[] key, System.Byte[] nonce, System.Byte[] cipherWithTag, System.Byte[]? aad = null)
    {
        if (key is null || key.Length != KEY32)
        {
            ThrowHelper.BadKeyLen();
        }

        if (nonce is null || nonce.Length != NONCE16)
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

    /// <summary>
    /// Fills a 32-byte Poly1305 one-time key using two CTR keystream blocks with counters 0 and 1.
    /// </summary>
    private static void FillPolyKeyCtr(
        System.ReadOnlySpan<System.Byte> key,
        System.ReadOnlySpan<System.Byte> nonce,
        System.Span<System.Byte> oneTimeKey32)
    {
        // block 0
        GenKeystreamBlock(key, nonce, 0UL, oneTimeKey32[..BLOCK16]);
        // block 1
        GenKeystreamBlock(key, nonce, 1UL, oneTimeKey32.Slice(BLOCK16, BLOCK16));
    }

    /// <summary>
    /// XORs <paramref name="src"/> with CTR keystream (starting from <paramref name="startCounter"/>) into <paramref name="dst"/>.
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
        System.Span<System.Byte> ks = stackalloc System.Byte[BLOCK16];

        // Preexpand Speck instance once for all blocks
        var speck = new Speck(key);

        while (offset < src.Length)
        {
            GenKeystreamBlock(speck, nonce, ctr, ks);
            System.Int32 take = System.Math.Min(BLOCK16, src.Length - offset);

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
    /// Generates one 16-byte CTR keystream block: Speck_Encrypt( nonce_128 + counter ) with 128-bit LE addition.
    /// (Overload that reuses an existing Speck instance to avoid re-expansion.)
    /// </summary>
    private static void GenKeystreamBlock(
        Speck speck,
        System.ReadOnlySpan<System.Byte> nonce,
        System.UInt64 counter,
        System.Span<System.Byte> out16)
    {
        // Read nonce into (low, high) 64-bit little-endian
        System.UInt64 n0 = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(nonce[..8]);
        System.UInt64 n1 = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(nonce.Slice(8, 8));

        // 128-bit addition (LE): (n0 + counter, n1 + carry)
        System.UInt64 ctrLow = n0 + counter;
        System.UInt64 carry = ctrLow < n0 ? 1UL : 0UL;
        System.UInt64 ctrHigh = n1 + carry;

        // Encrypt counter block with Speck
        System.UInt64 x = ctrLow;
        System.UInt64 y = ctrHigh;
        speck.EncryptBlock(ref x, ref y);

        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(out16[..8], x);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(out16.Slice(8, 8), y);
    }

    /// <summary>
    /// Generates one 16-byte CTR keystream block using a temporary Speck instance (for short one-off calls).
    /// </summary>
    private static void GenKeystreamBlock(
        System.ReadOnlySpan<System.Byte> key,
        System.ReadOnlySpan<System.Byte> nonce,
        System.UInt64 counter,
        System.Span<System.Byte> out16)
    {
        var speck = new Speck(key);
        GenKeystreamBlock(speck, nonce, counter, out16);
    }

    /// <summary>
    /// Updates Poly1305 with AEAD transcript and writes the final tag.
    /// </summary>
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

        Pad16(mac, aad.Length);

        if (!ciphertext.IsEmpty)
        {
            mac.Update(ciphertext);
        }

        Pad16(mac, ciphertext.Length);

        System.Span<System.Byte> lens = stackalloc System.Byte[16];
        WriteUInt64LEPair(lens, 0, (System.UInt64)aad.Length, (System.UInt64)ciphertext.Length);
        mac.Update(lens);

        mac.FinalizeTag(tagOut16);
        lens.Clear();
    }

    /// <summary>Zero padding to 16-byte boundary if needed.</summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void Pad16(Poly1305 mac, System.Int32 len)
    {
        System.Int32 rem = len & 0x0F;
        if (rem == 0)
        {
            return;
        }

        System.Span<System.Byte> pad = stackalloc System.Byte[16];
        pad[..(16 - rem)].Clear();
        mac.Update(pad[..(16 - rem)]);
    }

    /// <summary>Writes two little-endian UInt64 values into destination at offset.</summary>
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
        if (key.Length != KEY32)
        {
            ThrowHelper.BadKeyLen();
        }

        if (nonce.Length != NONCE16)
        {
            ThrowHelper.BadNonceLen();
        }
    }

    /// <summary>Centralized throw helpers (style aligned with your AEAD classes).</summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    private static class ThrowHelper
    {
        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void BadKeyLen() => throw new System.ArgumentException("Key must be 32 bytes (Speck 128/256).", "key");

        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void BadNonceLen() => throw new System.ArgumentException("Nonce must be 16 bytes.", "nonce");

        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void BadTagLen() => throw new System.ArgumentException("Tag must be 16 bytes.", "tag");

        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void OutputLenMismatch() => throw new System.ArgumentException("Output length must match input length.");

        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void CtPlusTagTooShort() => throw new System.ArgumentException("Ciphertext+Tag is too short.", "cipherWithTag");
    }

    #endregion
}
