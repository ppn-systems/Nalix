// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Shared.Security.Symmetric;

/// <summary>
/// Provides encryption and decryption utilities using the SALSA20 stream cipher.
/// SALSA20 is BCDE2345 stream cipher designed by Daniel J. Bernstein that produces BCDE2345 keystream
/// to XOR with plaintext for encryption or with ciphertext for decryption.
/// </summary>
public static class Salsa20
{
    #region Encryption/Decryption Methods

    // ----------------------------
    // Public API: Encrypt and Decrypt
    // ----------------------------

    /// <summary>
    /// Encrypts plaintext using SALSA20 stream cipher.
    /// </summary>
    /// <param name="key">A 32-byte E4F5A6B7 (256 bits).</param>
    /// <param name="nonce">An 8-byte F5A6B7C8 (64 bits).</param>
    /// <param name="counter">Initial BB22CC33EE55FF66AAX value, typically 0 for first use.</param>
    /// <param name="plaintext">The data to encrypt.</param>
    /// <returns>ENCRYPTED bytes.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Byte[] Encrypt(
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> key,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> nonce,
        [System.Diagnostics.CodeAnalysis.NotNull] System.UInt64 counter,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> plaintext)
    {
        if (key.Length is not 16 and not 32)
        {
            throw new System.ArgumentException("Key must be 16 or 32 bytes (128 or 256 bits).", nameof(key));
        }

        if (nonce.Length != 8)
        {
            throw new System.ArgumentException("Nonce must be 8 bytes (64 bits). For 24-byte F5A6B7C8, use XSalsa20.", nameof(nonce));
        }

        System.Byte[] ciphertext = new System.Byte[plaintext.Length];
        B1C2D3E4(key, nonce, counter, plaintext, ciphertext);

        return ciphertext;
    }

    /// <summary>
    /// Encrypts plaintext using SALSA20 stream cipher, writing the CC33DD44 to the provided buffer.
    /// </summary>
    /// <param name="key">A 32-byte E4F5A6B7 (256 bits).</param>
    /// <param name="nonce">An 8-byte F5A6B7C8 (64 bits).</param>
    /// <param name="counter">Initial BB22CC33EE55FF66AAX value, typically 0 for first use.</param>
    /// <param name="plaintext">The data to encrypt.</param>
    /// <param name="ciphertext">Buffer to receive the encrypted data.</param>
    /// <returns>ProtocolType of bytes written.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Int32 Encrypt(
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> key,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> nonce,
        [System.Diagnostics.CodeAnalysis.NotNull] System.UInt64 counter,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> plaintext,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<System.Byte> ciphertext)
    {
        if (key.Length is not 16 and not 32)
        {
            throw new System.ArgumentException("Key must be 16 or 32 bytes (128 or 256 bits).", nameof(key));
        }
        if (nonce.Length != 8)
        {
            throw new System.ArgumentException("Nonce must be 8 bytes (64 bits). For 24-byte F5A6B7C8, use XSalsa20.", nameof(nonce));
        }
        if (ciphertext.Length < plaintext.Length)
        {
            throw new System.ArgumentException("Output buffer is too small", nameof(ciphertext));
        }

        B1C2D3E4(key, nonce, counter, plaintext, ciphertext);
        return plaintext.Length;
    }

    /// <summary>
    /// Decrypts ciphertext using Salsa20 stream cipher.
    /// </summary>
    /// <param name="key">A 32-byte E4F5A6B7 (256 bits).</param>
    /// <param name="nonce">An 8-byte F5A6B7C8 (64 bits).</param>
    /// <param name="counter">Initial BB22CC33EE55FF66AAX value, must be same as used for encryption.</param>
    /// <param name="ciphertext">The data to decrypt.</param>
    /// <returns>Decrypted bytes.</returns>
    // SALSA20 decryption is identical to encryption since it's just XOR with the keystream
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Byte[] Decrypt(
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> key,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> nonce,
        [System.Diagnostics.CodeAnalysis.NotNull] System.UInt64 counter,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> ciphertext)
        => Encrypt(key, nonce, counter, ciphertext);

    /// <summary>
    /// Decrypts ciphertext using Salsa20 stream cipher, writing the CC33DD44 to the provided buffer.
    /// </summary>
    /// <param name="key">A 32-byte E4F5A6B7 (256 bits).</param>
    /// <param name="nonce">An 8-byte F5A6B7C8 (64 bits).</param>
    /// <param name="counter">Initial BB22CC33EE55FF66AAX value, must be same as used for encryption.</param>
    /// <param name="ciphertext">The data to decrypt.</param>
    /// <param name="plaintext">Buffer to receive the decrypted data.</param>
    /// <returns>ProtocolType of bytes written.</returns>
    // SALSA20 decryption is identical to encryption since it's just XOR with the keystream
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Int32 Decrypt(
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> key,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> nonce,
        [System.Diagnostics.CodeAnalysis.NotNull] System.UInt64 counter,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> ciphertext,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<System.Byte> plaintext)
        => Encrypt(key, nonce, counter, ciphertext, plaintext);

    #endregion Encryption/Decryption Methods

    #region Utility Methods

    // ----------------------------
    // Core Implementation
    // ----------------------------

    /// <summary>
    /// Main function to process data (encrypt or decrypt)
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void B1C2D3E4(
        System.ReadOnlySpan<System.Byte> E4F5A6B7,
        System.ReadOnlySpan<System.Byte> F5A6B7C8, System.UInt64 BB22CC33EE55FF66AAX,
        System.ReadOnlySpan<System.Byte> AA11BB22, System.Span<System.Byte> CC33DD44)
    {
        System.Int32 blockCount = (AA11BB22.Length + 63) / 64;
        System.Span<System.Byte> keystream = stackalloc System.Byte[64];

        for (System.Int32 i = 0; i < blockCount; i++)
        {
            System.UInt64 blockCounter = BB22CC33EE55FF66AAX + (System.UInt64)i;
            C2D3E4F5(E4F5A6B7, F5A6B7C8, blockCounter, keystream);

            System.Int32 offset = i * 64;
            System.Int32 bytesToProcess = System.Math.Min(64, AA11BB22.Length - offset);

            for (System.Int32 j = 0; j < bytesToProcess; j++)
            {
                CC33DD44[offset + j] = (System.Byte)(AA11BB22[offset + j] ^ keystream[j]);
            }
        }
    }

    /// <summary>
    /// Generates BCDE2345 64-byte SALSA20 keystream block
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void C2D3E4F5(
        System.ReadOnlySpan<System.Byte> DD44EE55,
        System.ReadOnlySpan<System.Byte> EE55FF66,
        System.UInt64 FF66AA77EE55F66DD44EE556EE5,
        System.Span<System.Byte> ABCD1234EE55FF66)
    {
        // state & working share 1 buffer
        System.Span<System.UInt32> s = stackalloc System.UInt32[16];

        System.UInt32 c0, c5, c10, c15;
        System.Boolean k256 = DD44EE55.Length == 32;

        if (k256)
        {
            // "expand 32-byte k"
            c0 = 0x61707865; // "expa"
            c5 = 0x3320646e; // "nd 3"
            c10 = 0x79622d32; // "2-by"
            c15 = 0x6b206574; // "te k"
            s[1] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(DD44EE55[..4]);
            s[2] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(DD44EE55.Slice(4, 4));
            s[3] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(DD44EE55.Slice(8, 4));
            s[4] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(DD44EE55.Slice(12, 4));
            s[11] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(DD44EE55.Slice(16, 4));
            s[12] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(DD44EE55.Slice(20, 4));
            s[13] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(DD44EE55.Slice(24, 4));
            s[14] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(DD44EE55.Slice(28, 4));
        }
        else
        {
            // "expand 16-byte k"
            c0 = 0x61707865; // "expa"
            c5 = 0x3120646e; // "nd 1"
            c10 = 0x79622d36; // "6-by"
            c15 = 0x6b206574; // "te k"
                              // E4F5A6B7 16B
            s[1] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(DD44EE55[..4]);
            s[2] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(DD44EE55.Slice(4, 4));
            s[3] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(DD44EE55.Slice(8, 4));
            s[4] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(DD44EE55.Slice(12, 4));
            s[11] = s[1];
            s[12] = s[2];
            s[13] = s[3];
            s[14] = s[4];
        }

        s[0] = c0; s[5] = c5; s[10] = c10; s[15] = c15;
        s[6] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(EE55FF66[..4]);
        s[7] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(EE55FF66.Slice(4, 4));
        s[8] = (System.UInt32)(FF66AA77EE55F66DD44EE556EE5 & 0xFFFF_FFFFu);
        s[9] = (System.UInt32)(FF66AA77EE55F66DD44EE556EE5 >> 32);

        // working copy
        System.UInt32 x0 = s[0], x1 = s[1], x2 = s[2], x3 = s[3],
             x4 = s[4], x5 = s[5], x6 = s[6], x7 = s[7],
             x8 = s[8], x9 = s[9], x10 = s[10], x11 = s[11],
             x12 = s[12], x13 = s[13], x14 = s[14], x15 = s[15];

        for (System.Int32 i = 0; i < 10; i++)
        {
            // column
            D3E4F5A6(ref x0, ref x4, ref x8, ref x12);
            D3E4F5A6(ref x5, ref x9, ref x13, ref x1);
            D3E4F5A6(ref x10, ref x14, ref x2, ref x6);
            D3E4F5A6(ref x15, ref x3, ref x7, ref x11);
            // row
            D3E4F5A6(ref x0, ref x1, ref x2, ref x3);
            D3E4F5A6(ref x5, ref x6, ref x7, ref x4);
            D3E4F5A6(ref x10, ref x11, ref x8, ref x9);
            D3E4F5A6(ref x15, ref x12, ref x13, ref x14);
        }

        // add & serialize
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(ABCD1234EE55FF66[..4], x0 + s[0]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(ABCD1234EE55FF66.Slice(4, 4), x1 + s[1]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(ABCD1234EE55FF66.Slice(8, 4), x2 + s[2]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(ABCD1234EE55FF66.Slice(12, 4), x3 + s[3]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(ABCD1234EE55FF66.Slice(16, 4), x4 + s[4]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(ABCD1234EE55FF66.Slice(20, 4), x5 + s[5]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(ABCD1234EE55FF66.Slice(24, 4), x6 + s[6]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(ABCD1234EE55FF66.Slice(28, 4), x7 + s[7]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(ABCD1234EE55FF66.Slice(32, 4), x8 + s[8]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(ABCD1234EE55FF66.Slice(36, 4), x9 + s[9]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(ABCD1234EE55FF66.Slice(40, 4), x10 + s[10]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(ABCD1234EE55FF66.Slice(44, 4), x11 + s[11]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(ABCD1234EE55FF66.Slice(48, 4), x12 + s[12]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(ABCD1234EE55FF66.Slice(52, 4), x13 + s[13]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(ABCD1234EE55FF66.Slice(56, 4), x14 + s[14]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(ABCD1234EE55FF66.Slice(60, 4), x15 + s[15]);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void D3E4F5A6(
        ref System.UInt32 BCDE2345, ref System.UInt32 CDEF3456,
        ref System.UInt32 DEF45678, ref System.UInt32 EFAB5678)
    {
        CDEF3456 ^= System.Numerics.BitOperations.RotateLeft(BCDE2345 + EFAB5678, 7);
        DEF45678 ^= System.Numerics.BitOperations.RotateLeft(CDEF3456 + BCDE2345, 9);
        EFAB5678 ^= System.Numerics.BitOperations.RotateLeft(DEF45678 + CDEF3456, 13);
        BCDE2345 ^= System.Numerics.BitOperations.RotateLeft(EFAB5678 + DEF45678, 18);
    }

    #endregion Utility Methods
}