// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Shared.Security.Symmetric;

/// <summary>
/// Provides encryption and decryption utilities using the Salsa20 stream cipher.
/// Salsa20 is a stream cipher designed by Daniel J. Bernstein that produces a keystream
/// to XOR with plaintext for encryption or with ciphertext for decryption.
/// </summary>
/// <remarks>
/// See <see href="https://cr.yp.to/snuffle/spec.pdf">Salsa20 Specification</see> for details.
/// </remarks>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public static class Salsa20
{
    /// <summary>
    /// Required nonce length in bytes (64-bit nonce).
    /// </summary>
    public const System.Byte NonceSize = 8;

    #region Encryption/Decryption Methods

    /// <summary>
    /// Encrypts plaintext using the Salsa20 stream cipher, writing the result to a provided buffer.
    /// </summary>
    /// <param name="key">A 16-byte (128-bit) or 32-byte (256-bit) key.</param>
    /// <param name="nonce">An 8-byte (64-bit) nonce.</param>
    /// <param name="counter">Initial 64-bit block counter value, typically 0.</param>
    /// <param name="plaintext">The data to encrypt.</param>
    /// <param name="ciphertext">Buffer to receive the encrypted data; must be at least as large as plaintext.</param>
    /// <returns>Number of bytes written to <paramref name="ciphertext"/>.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Int32 Encrypt(
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> key,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> nonce,
        System.UInt64 counter,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> plaintext,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<System.Byte> ciphertext)
    {
        ValidateKeyAndNonce(key, nonce);

        if (ciphertext.Length < plaintext.Length)
        {
            throw new System.ArgumentException("Output buffer is too small.", nameof(ciphertext));
        }

        ProcessData(key, nonce, counter, plaintext, ciphertext);
        return plaintext.Length;
    }

    /// <summary>
    /// Decrypts ciphertext using the Salsa20 stream cipher, writing the result to a provided buffer.
    /// </summary>
    /// <param name="key">A 16-byte (128-bit) or 32-byte (256-bit) key.</param>
    /// <param name="nonce">An 8-byte (64-bit) nonce; must match the value used during encryption.</param>
    /// <param name="counter">Initial 64-bit block counter; must match the value used during encryption.</param>
    /// <param name="ciphertext">The data to decrypt.</param>
    /// <param name="plaintext">Buffer to receive the decrypted data.</param>
    /// <returns>Number of bytes written to <paramref name="plaintext"/>.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Int32 Decrypt(
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> key,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> nonce,
        System.UInt64 counter,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> ciphertext,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<System.Byte> plaintext)
        => Encrypt(key, nonce, counter, ciphertext, plaintext);

    #endregion Encryption/Decryption Methods

    #region Private Helpers

    /// <summary>
    /// Validates that <paramref name="key"/> is 16 or 32 bytes and <paramref name="nonce"/> is 8 bytes.
    /// </summary>
    private static void ValidateKeyAndNonce(
        System.ReadOnlySpan<System.Byte> key,
        System.ReadOnlySpan<System.Byte> nonce)
    {
        if (key.Length is not 16 and not 32)
        {
            throw new System.ArgumentException("Key must be 16 or 32 bytes (128 or 256 bits).", nameof(key));
        }

        if (nonce.Length != NonceSize)
        {
            throw new System.ArgumentException($"Nonce must be {NonceSize} bytes (64 bits). For 24-byte nonces, use XSalsa20.", nameof(nonce));
        }
    }

    /// <summary>
    /// Core Salsa20 processing: generates keystream block by block and XORs with input data.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void ProcessData(
        System.ReadOnlySpan<System.Byte> key,
        System.ReadOnlySpan<System.Byte> nonce,
        System.UInt64 counter,
        System.ReadOnlySpan<System.Byte> input,
        System.Span<System.Byte> output)
    {
        System.Int32 blockCount = (input.Length + 63) / 64;
        System.Span<System.Byte> keystream = stackalloc System.Byte[64];

        for (System.Int32 i = 0; i < blockCount; i++)
        {
            System.UInt64 blockCounter = counter + (System.UInt64)i;
            GenerateKeystreamBlock(key, nonce, blockCounter, keystream);

            System.Int32 offset = i * 64;
            System.Int32 bytesToProcess = System.Math.Min(64, input.Length - offset);

            for (System.Int32 j = 0; j < bytesToProcess; j++)
            {
                output[offset + j] = (System.Byte)(input[offset + j] ^ keystream[j]);
            }
        }
    }

    /// <summary>
    /// Generates a single 64-byte Salsa20 keystream block.
    /// </summary>
    /// <param name="key">16 or 32 byte key.</param>
    /// <param name="nonce">8-byte nonce.</param>
    /// <param name="counter">64-bit block counter (low 32 bits = s[8], high 32 bits = s[9]).</param>
    /// <param name="output">64-byte output buffer to receive the keystream block.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void GenerateKeystreamBlock(
        System.ReadOnlySpan<System.Byte> key,
        System.ReadOnlySpan<System.Byte> nonce,
        System.UInt64 counter,
        System.Span<System.Byte> output)
    {
        System.Span<System.UInt32> s = stackalloc System.UInt32[16];

        // Determine constants based on key length (per Salsa20 spec)
        System.UInt32 c0, c5, c10, c15;
        System.Boolean is256BitKey = key.Length == 32;

        if (is256BitKey)
        {
            // "expand 32-byte k"
            c0 = 0x61707865; // "expa"
            c5 = 0x3320646e; // "nd 3"
            c10 = 0x79622d32; // "2-by"
            c15 = 0x6b206574; // "te k"

            // First 16 bytes of key → s[1..4]
            s[1] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(key[..4]);
            s[2] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(4, 4));
            s[3] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(8, 4));
            s[4] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(12, 4));
            // Second 16 bytes of key → s[11..14]
            s[11] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(16, 4));
            s[12] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(20, 4));
            s[13] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(24, 4));
            s[14] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(28, 4));
        }
        else
        {
            // "expand 16-byte k"
            c0 = 0x61707865; // "expa"
            c5 = 0x3120646e; // "nd 1"
            c10 = 0x79622d36; // "6-by"
            c15 = 0x6b206574; // "te k"

            // Key is repeated in both halves
            s[1] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(key[..4]);
            s[2] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(4, 4));
            s[3] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(8, 4));
            s[4] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(12, 4));
            s[11] = s[1];
            s[12] = s[2];
            s[13] = s[3];
            s[14] = s[4];
        }

        // Place constants
        s[0] = c0;
        s[5] = c5;
        s[10] = c10;
        s[15] = c15;

        // Nonce → s[6..7]
        s[6] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(nonce[..4]);
        s[7] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(nonce.Slice(4, 4));

        // Counter → s[8] (low 32 bits), s[9] (high 32 bits)
        s[8] = (System.UInt32)(counter & 0xFFFF_FFFFu);
        s[9] = (System.UInt32)(counter >> 32);

        // Copy state into working variables
        System.UInt32 x0 = s[0], x1 = s[1], x2 = s[2], x3 = s[3],
                      x4 = s[4], x5 = s[5], x6 = s[6], x7 = s[7],
                      x8 = s[8], x9 = s[9], x10 = s[10], x11 = s[11],
                      x12 = s[12], x13 = s[13], x14 = s[14], x15 = s[15];

        // 20 rounds = 10 double-rounds (column then row)
        for (System.Int32 i = 0; i < 10; i++)
        {
            // Column rounds (down each column of the 4x4 matrix)
            QuarterRound(ref x0, ref x4, ref x8, ref x12);
            QuarterRound(ref x5, ref x9, ref x13, ref x1);
            QuarterRound(ref x10, ref x14, ref x2, ref x6);
            QuarterRound(ref x15, ref x3, ref x7, ref x11);
            // Row rounds (across each row)
            QuarterRound(ref x0, ref x1, ref x2, ref x3);
            QuarterRound(ref x5, ref x6, ref x7, ref x4);
            QuarterRound(ref x10, ref x11, ref x8, ref x9);
            QuarterRound(ref x15, ref x12, ref x13, ref x14);
        }

        // Add original state and serialize as little-endian
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(output[..4], x0 + s[0]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(4, 4), x1 + s[1]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(8, 4), x2 + s[2]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(12, 4), x3 + s[3]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(16, 4), x4 + s[4]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(20, 4), x5 + s[5]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(24, 4), x6 + s[6]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(28, 4), x7 + s[7]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(32, 4), x8 + s[8]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(36, 4), x9 + s[9]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(40, 4), x10 + s[10]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(44, 4), x11 + s[11]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(48, 4), x12 + s[12]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(52, 4), x13 + s[13]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(56, 4), x14 + s[14]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(60, 4), x15 + s[15]);
    }

    /// <summary>
    /// Salsa20 Quarter Round operation (per Salsa20 specification).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void QuarterRound(
        ref System.UInt32 a, ref System.UInt32 b,
        ref System.UInt32 c, ref System.UInt32 d)
    {
        b ^= System.Numerics.BitOperations.RotateLeft(a + d, 7);
        c ^= System.Numerics.BitOperations.RotateLeft(b + a, 9);
        d ^= System.Numerics.BitOperations.RotateLeft(c + b, 13);
        a ^= System.Numerics.BitOperations.RotateLeft(d + c, 18);
    }

    #endregion Private Helpers
}