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
        if (key.Length != 32)
        {
            throw new ArgumentException("Key must be 32 bytes (256 bits)", nameof(key));
        }

        if (nonce.Length != 8)
        {
            throw new ArgumentException("Nonce must be 8 bytes (64 bits)", nameof(nonce));
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
        // Initialize the state with constants, key, counter, and nonce
        Span<UInt32> state = stackalloc UInt32[16];

        // Performance "expand 32-byte k"
        state[0] = 0x61707865; // "expa"
        state[5] = 0x3320646e; // "nd 3"
        state[10] = 0x79622d32; // "2-by"
        state[15] = 0x6b206574; // "te k"

        // Key (first half)
        state[1] = BinaryPrimitives.ReadUInt32LittleEndian(key[..4]);
        state[2] = BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(4, 4));
        state[3] = BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(8, 4));
        state[4] = BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(12, 4));

        // Key (second half)
        state[11] = BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(16, 4));
        state[12] = BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(20, 4));
        state[13] = BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(24, 4));
        state[14] = BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(28, 4));

        // Counter (64 bits split into two 32-bit words)
        state[8] = (UInt32)(counter & 0xFFFFFFFF);
        state[9] = (UInt32)(counter >> 32);

        // Nonce (64 bits split into two 32-bit words)
        state[6] = BinaryPrimitives.ReadUInt32LittleEndian(nonce[..4]);
        state[7] = BinaryPrimitives.ReadUInt32LittleEndian(nonce.Slice(4, 4));

        // Create a working copy of the state
        Span<UInt32> workingState = stackalloc UInt32[16];
        state.CopyTo(workingState);

        // Apply the Salsa20 core function (20 rounds)
        for (Int32 i = 0; i < 10; i++)
        {
            // Column rounds
            QuarterRound(ref workingState[0], ref workingState[4], ref workingState[8], ref workingState[12]);
            QuarterRound(ref workingState[5], ref workingState[9], ref workingState[13], ref workingState[1]);
            QuarterRound(ref workingState[10], ref workingState[14], ref workingState[2], ref workingState[6]);
            QuarterRound(ref workingState[15], ref workingState[3], ref workingState[7], ref workingState[11]);

            // Row rounds
            QuarterRound(ref workingState[0], ref workingState[1], ref workingState[2], ref workingState[3]);
            QuarterRound(ref workingState[5], ref workingState[6], ref workingState[7], ref workingState[4]);
            QuarterRound(ref workingState[10], ref workingState[11], ref workingState[8], ref workingState[9]);
            QuarterRound(ref workingState[15], ref workingState[12], ref workingState[13], ref workingState[14]);
        }

        // Push the original state to the worked state and serialize to output
        for (Int32 i = 0; i < 16; i++)
        {
            UInt32 result = workingState[i] + state[i];
            BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(i * 4, 4), result);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void QuarterRound(ref UInt32 a, ref UInt32 b, ref UInt32 c, ref UInt32 d)
    {
        b ^= BitwiseUtils.RotateLeft(a + d, 7);
        c ^= BitwiseUtils.RotateLeft(b + a, 9);
        d ^= BitwiseUtils.RotateLeft(c + b, 13);
        a ^= BitwiseUtils.RotateLeft(d + c, 18);
    }

    #endregion Utility Methods
}