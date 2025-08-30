// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Cryptography.Primitives;
using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace Nalix.Cryptography.Symmetric.Stream;

/// <summary>
/// Provides encryption and decryption utilities using the Salsa20 stream cipher.
/// Salsa20 is a stream cipher designed by Daniel J. Bernstein that produces a keystream
/// to XOR with plaintext for encryption or with ciphertext for decryption.
/// </summary>
public static class Salsa20
{
    #region Encryption/Decryption Methods

    // ----------------------------
    // Public API: Encrypt and Decrypt
    // ----------------------------

    /// <summary>
    /// Encrypts plaintext using Salsa20 stream cipher.
    /// </summary>
    /// <param name="key">A 32-byte key (256 bits).</param>
    /// <param name="nonce">An 8-byte nonce (64 bits).</param>
    /// <param name="counter">Initial counter value, typically 0 for first use.</param>
    /// <param name="plaintext">The data to encrypt.</param>
    /// <returns>Encrypted bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Byte[] Encrypt(
        ReadOnlySpan<Byte> key, ReadOnlySpan<Byte> nonce,
        UInt64 counter, ReadOnlySpan<Byte> plaintext)
    {
        ValidateParameters(key, nonce);
        Byte[] ciphertext = new Byte[plaintext.Length];
        ProcessData(key, nonce, counter, plaintext, ciphertext);
        return ciphertext;
    }

    /// <summary>
    /// Encrypts plaintext using Salsa20 stream cipher, writing the output to the provided buffer.
    /// </summary>
    /// <param name="key">A 32-byte key (256 bits).</param>
    /// <param name="nonce">An 8-byte nonce (64 bits).</param>
    /// <param name="counter">Initial counter value, typically 0 for first use.</param>
    /// <param name="plaintext">The data to encrypt.</param>
    /// <param name="ciphertext">Buffer to receive the encrypted data.</param>
    /// <returns>TransportProtocol of bytes written.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Int32 Encrypt(
        ReadOnlySpan<Byte> key, ReadOnlySpan<Byte> nonce, UInt64 counter,
        ReadOnlySpan<Byte> plaintext, Span<Byte> ciphertext)
    {
        ValidateParameters(key, nonce);
        if (ciphertext.Length < plaintext.Length)
        {
            throw new ArgumentException("Output buffer is too small", nameof(ciphertext));
        }

        ProcessData(key, nonce, counter, plaintext, ciphertext);
        return plaintext.Length;
    }

    /// <summary>
    /// Decrypts ciphertext using Salsa20 stream cipher.
    /// </summary>
    /// <param name="key">A 32-byte key (256 bits).</param>
    /// <param name="nonce">An 8-byte nonce (64 bits).</param>
    /// <param name="counter">Initial counter value, must be same as used for encryption.</param>
    /// <param name="ciphertext">The data to decrypt.</param>
    /// <returns>Decrypted bytes.</returns>
    // Salsa20 decryption is identical to encryption since it's just XOR with the keystream
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Byte[] Decrypt(ReadOnlySpan<Byte> key, ReadOnlySpan<Byte> nonce,
        UInt64 counter, ReadOnlySpan<Byte> ciphertext) => Encrypt(key, nonce, counter, ciphertext);

    /// <summary>
    /// Decrypts ciphertext using Salsa20 stream cipher, writing the output to the provided buffer.
    /// </summary>
    /// <param name="key">A 32-byte key (256 bits).</param>
    /// <param name="nonce">An 8-byte nonce (64 bits).</param>
    /// <param name="counter">Initial counter value, must be same as used for encryption.</param>
    /// <param name="ciphertext">The data to decrypt.</param>
    /// <param name="plaintext">Buffer to receive the decrypted data.</param>
    /// <returns>TransportProtocol of bytes written.</returns>
    // Salsa20 decryption is identical to encryption since it's just XOR with the keystream
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Int32 Decrypt(ReadOnlySpan<Byte> key, ReadOnlySpan<Byte> nonce, UInt64 counter,
        ReadOnlySpan<Byte> ciphertext, Span<Byte> plaintext) => Encrypt(key, nonce, counter, ciphertext, plaintext);

    #endregion Encryption/Decryption Methods

    #region Utility Methods

    /// <summary>
    /// Converts a string passphrase into a 32-byte key using a simple hash function.
    /// Note: This is not a cryptographically secure KDF and should not be used for
    /// sensitive applications. Use a proper KDF like PBKDF2, Argon2, or Scrypt instead.
    /// </summary>
    /// <param name="passphrase">The passphrase to convert.</param>
    /// <returns>A 32-byte key derived from the passphrase.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Byte[] DeriveKeyFromPassphrase(String passphrase)
    {
        if (String.IsNullOrEmpty(passphrase))
        {
            throw new ArgumentException("Passphrase cannot be null or empty", nameof(passphrase));
        }

        // Simple hash function to derive a key (NOT secure for real use!)
        Byte[] bytes = System.Text.Encoding.UTF8.GetBytes(passphrase);
        Byte[] key = new Byte[32];

        // Simple stretching algorithm (NOT secure for real applications!)
        for (Int32 i = 0; i < 1000; i++)
        {
            for (Int32 j = 0; j < bytes.Length; j++)
            {
                key[j % 32] ^= (Byte)(bytes[j] + i);
                key[(j + 1) % 32] = (Byte)((key[(j + 1) % 32] + bytes[j]) & 0xFF);
            }

            // Mix further
            for (Int32 j = 0; j < 32; j++)
            {
                key[j] = (Byte)((key[j] + key[(j + 1) % 32]) & 0xFF);
            }
        }

        return key;
    }

    // ----------------------------
    // Core Implementation
    // ----------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ValidateParameters(ReadOnlySpan<Byte> key, ReadOnlySpan<Byte> nonce)
    {
        if (key.Length is not 16 and not 32)
        {
            throw new ArgumentException("Key must be 16 or 32 bytes (128 or 256 bits).", nameof(key));
        }

        if (nonce.Length != 8)
        {
            throw new ArgumentException("Nonce must be 8 bytes (64 bits). For 24-byte nonce, use XSalsa20.", nameof(nonce));
        }
    }

    /// <summary>
    /// Main function to process data (encrypt or decrypt)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ProcessData(ReadOnlySpan<Byte> key, ReadOnlySpan<Byte> nonce, UInt64 counter,
                                   ReadOnlySpan<Byte> input, Span<Byte> output)
    {
        Int32 blockCount = (input.Length + 63) / 64;
        Span<Byte> keystream = stackalloc Byte[64];

        for (Int32 i = 0; i < blockCount; i++)
        {
            UInt64 blockCounter = counter + (UInt64)i;
            GenerateSalsaBlock(key, nonce, blockCounter, keystream);

            Int32 offset = i * 64;
            Int32 bytesToProcess = Math.Min(64, input.Length - offset);

            for (Int32 j = 0; j < bytesToProcess; j++)
            {
                output[offset + j] = (Byte)(input[offset + j] ^ keystream[j]);
            }
        }
    }

    /// <summary>
    /// Generates a 64-byte Salsa20 keystream block
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void GenerateSalsaBlock(ReadOnlySpan<Byte> key, ReadOnlySpan<Byte> nonce, UInt64 counter, Span<Byte> output)
    {
        // state & working share 1 buffer
        Span<UInt32> s = stackalloc UInt32[16];

        UInt32 c0, c5, c10, c15;
        Boolean k256 = key.Length == 32;

        if (k256)
        {
            // "expand 32-byte k"
            c0 = 0x61707865; // "expa"
            c5 = 0x3320646e; // "nd 3"
            c10 = 0x79622d32; // "2-by"
            c15 = 0x6b206574; // "te k"
            s[1] = BinaryPrimitives.ReadUInt32LittleEndian(key[..4]);
            s[2] = BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(4, 4));
            s[3] = BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(8, 4));
            s[4] = BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(12, 4));
            s[11] = BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(16, 4));
            s[12] = BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(20, 4));
            s[13] = BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(24, 4));
            s[14] = BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(28, 4));
        }
        else
        {
            // "expand 16-byte k"
            c0 = 0x61707865; // "expa"
            c5 = 0x3120646e; // "nd 1"
            c10 = 0x79622d36; // "6-by"
            c15 = 0x6b206574; // "te k"
                              // key 16B
            s[1] = BinaryPrimitives.ReadUInt32LittleEndian(key[..4]);
            s[2] = BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(4, 4));
            s[3] = BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(8, 4));
            s[4] = BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(12, 4));
            s[11] = s[1];
            s[12] = s[2];
            s[13] = s[3];
            s[14] = s[4];
        }

        s[0] = c0; s[5] = c5; s[10] = c10; s[15] = c15;
        s[6] = BinaryPrimitives.ReadUInt32LittleEndian(nonce[..4]);
        s[7] = BinaryPrimitives.ReadUInt32LittleEndian(nonce.Slice(4, 4));
        s[8] = (UInt32)(counter & 0xFFFF_FFFFu);
        s[9] = (UInt32)(counter >> 32);

        // working copy
        UInt32 x0 = s[0], x1 = s[1], x2 = s[2], x3 = s[3],
             x4 = s[4], x5 = s[5], x6 = s[6], x7 = s[7],
             x8 = s[8], x9 = s[9], x10 = s[10], x11 = s[11],
             x12 = s[12], x13 = s[13], x14 = s[14], x15 = s[15];

        for (Int32 i = 0; i < 10; i++)
        {
            // column
            QuarterRound(ref x0, ref x4, ref x8, ref x12);
            QuarterRound(ref x5, ref x9, ref x13, ref x1);
            QuarterRound(ref x10, ref x14, ref x2, ref x6);
            QuarterRound(ref x15, ref x3, ref x7, ref x11);
            // row
            QuarterRound(ref x0, ref x1, ref x2, ref x3);
            QuarterRound(ref x5, ref x6, ref x7, ref x4);
            QuarterRound(ref x10, ref x11, ref x8, ref x9);
            QuarterRound(ref x15, ref x12, ref x13, ref x14);
        }

        // add & serialize
        BinaryPrimitives.WriteUInt32LittleEndian(output[..4], x0 + s[0]);
        BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(4, 4), x1 + s[1]);
        BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(8, 4), x2 + s[2]);
        BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(12, 4), x3 + s[3]);
        BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(16, 4), x4 + s[4]);
        BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(20, 4), x5 + s[5]);
        BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(24, 4), x6 + s[6]);
        BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(28, 4), x7 + s[7]);
        BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(32, 4), x8 + s[8]);
        BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(36, 4), x9 + s[9]);
        BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(40, 4), x10 + s[10]);
        BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(44, 4), x11 + s[11]);
        BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(48, 4), x12 + s[12]);
        BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(52, 4), x13 + s[13]);
        BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(56, 4), x14 + s[14]);
        BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(60, 4), x15 + s[15]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void QuarterRound(ref UInt32 a, ref UInt32 b, ref UInt32 c, ref UInt32 d)
    {
        b ^= BitwiseUtils.RotateLeft(a + d, 7);
        c ^= BitwiseUtils.RotateLeft(b + a, 9);
        d ^= BitwiseUtils.RotateLeft(c + b, 13);
        a ^= BitwiseUtils.RotateLeft(d + c, 18);
    }

    #endregion Utility Methods
}