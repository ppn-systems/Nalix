using Nalix.Cryptography.Internal;
using System;
using System.Runtime.CompilerServices;

namespace Nalix.Cryptography.Symmetric.Block;

/// <summary>
/// Implementation of the Speck lightweight block cipher developed by the NSA.
/// This implementation does not rely on system cryptographic libraries for its core algorithm,
/// but uses a secure random number generator for IV generation when necessary.
/// </summary>
[SkipLocalsInit]
public static unsafe class Speck
{
    #region Constants

    // Speck 64/128 — 64-bit block with a 128-bit key
    private const Int32 ROUNDS = 27;

    private const Int32 BLOCK_SIZE_BYTES = 8;  // 64 bits = 8 bytes
    private const Int32 KEY_SIZE_BYTES = 16;   // 128 bits = 16 bytes

    #endregion Constants

    #region Public Methods

    /// <summary>
    /// Encrypts a single 8-byte (64-bit) block using the Speck cipher.
    /// </summary>
    /// <param name="plaintext">The input data to encrypt (8 bytes).</param>
    /// <param name="key">The encryption key (16 bytes).</param>
    /// <returns>The encrypted ciphertext (8 bytes).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Byte[] Encrypt(Byte[] plaintext, Byte[] key)
    {
        if (plaintext.Length != BLOCK_SIZE_BYTES)
        {
            throw new ArgumentException($"Plaintext must be exactly {BLOCK_SIZE_BYTES} bytes.", nameof(plaintext));
        }

        if (key.Length != KEY_SIZE_BYTES)
        {
            throw new ArgumentException($"Key must be exactly {KEY_SIZE_BYTES} bytes.", nameof(key));
        }

        UInt32[] roundKeys = ExpandKey(key);
        Byte[] ciphertext = new Byte[BLOCK_SIZE_BYTES];

        fixed (Byte* plaintextPtr = plaintext)
        fixed (Byte* ciphertextPtr = ciphertext)
        fixed (UInt32* roundKeysPtr = roundKeys)
        {
            // Load plaintext into two 32-bit halves
            UInt32 x = *(UInt32*)plaintextPtr;
            UInt32 y = *(UInt32*)(plaintextPtr + 4);

            // Encrypt
            EncryptBlock(ref x, ref y, roundKeysPtr);

            // Store the result back into bytes
            *(UInt32*)ciphertextPtr = x;
            *(UInt32*)(ciphertextPtr + 4) = y;
        }

        return ciphertext;
    }

    /// <summary>
    /// Decrypts a single 8-byte (64-bit) block encrypted with the Speck cipher.
    /// </summary>
    /// <param name="ciphertext">The encrypted data to decrypt (8 bytes).</param>
    /// <param name="key">The decryption key (16 bytes).</param>
    /// <returns>The original plaintext (8 bytes).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Byte[] Decrypt(Byte[] ciphertext, Byte[] key)
    {
        if (ciphertext.Length != BLOCK_SIZE_BYTES)
        {
            throw new ArgumentException($"Ciphertext must be exactly {BLOCK_SIZE_BYTES} bytes.", nameof(ciphertext));
        }

        if (key.Length != KEY_SIZE_BYTES)
        {
            throw new ArgumentException($"Key must be exactly {KEY_SIZE_BYTES} bytes.", nameof(key));
        }

        UInt32[] roundKeys = ExpandKey(key);
        Byte[] plaintext = new Byte[BLOCK_SIZE_BYTES];

        fixed (Byte* ciphertextPtr = ciphertext)
        fixed (Byte* plaintextPtr = plaintext)
        fixed (UInt32* roundKeysPtr = roundKeys)
        {
            // Load ciphertext into two 32-bit halves
            UInt32 x = *(UInt32*)ciphertextPtr;
            UInt32 y = *(UInt32*)(ciphertextPtr + 4);

            // Decrypt
            DecryptBlock(ref x, ref y, roundKeysPtr);

            // Store the result back into bytes
            *(UInt32*)plaintextPtr = x;
            *(UInt32*)(plaintextPtr + 4) = y;
        }

        return plaintext;
    }

    /// <summary>
    /// Encrypts a 64-bit block using Span input and output.
    /// </summary>
    /// <param name="plaintext">The input data (8 bytes).</param>
    /// <param name="key">The encryption key (16 bytes).</param>
    /// <param name="output">The destination span to write the ciphertext.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Encrypt(ReadOnlySpan<Byte> plaintext, ReadOnlySpan<Byte> key, Span<Byte> output)
    {
        if (plaintext.Length != BLOCK_SIZE_BYTES)
        {
            throw new ArgumentException($"Plaintext must be exactly {BLOCK_SIZE_BYTES} bytes.", nameof(plaintext));
        }

        if (key.Length != KEY_SIZE_BYTES)
        {
            throw new ArgumentException($"Key must be exactly {KEY_SIZE_BYTES} bytes.", nameof(key));
        }

        if (output.Length != BLOCK_SIZE_BYTES)
        {
            throw new ArgumentException($"Output must be exactly {BLOCK_SIZE_BYTES} bytes.", nameof(output));
        }

        UInt32[] roundKeys = ExpandKey(key);

        fixed (Byte* plaintextPtr = plaintext)
        fixed (Byte* outputPtr = output)
        fixed (UInt32* roundKeysPtr = roundKeys)
        {
            UInt32 x = *(UInt32*)plaintextPtr;
            UInt32 y = *(UInt32*)(plaintextPtr + 4);

            EncryptBlock(ref x, ref y, roundKeysPtr);

            *(UInt32*)outputPtr = x;
            *(UInt32*)(outputPtr + 4) = y;
        }
    }

    /// <summary>
    /// Decrypts a 64-bit block using Span input and output.
    /// </summary>
    /// <param name="ciphertext">The encrypted input data (8 bytes).</param>
    /// <param name="key">The decryption key (16 bytes).</param>
    /// <param name="output">The destination span to write the plaintext.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Decrypt(ReadOnlySpan<Byte> ciphertext, ReadOnlySpan<Byte> key, Span<Byte> output)
    {
        if (ciphertext.Length != BLOCK_SIZE_BYTES)
        {
            throw new ArgumentException($"Ciphertext must be exactly {BLOCK_SIZE_BYTES} bytes.", nameof(ciphertext));
        }

        if (key.Length != KEY_SIZE_BYTES)
        {
            throw new ArgumentException($"Key must be exactly {KEY_SIZE_BYTES} bytes.", nameof(key));
        }

        if (output.Length != BLOCK_SIZE_BYTES)
        {
            throw new ArgumentException($"Output must be exactly {BLOCK_SIZE_BYTES} bytes.", nameof(output));
        }

        UInt32[] roundKeys = ExpandKey(key);

        fixed (Byte* ciphertextPtr = ciphertext)
        fixed (Byte* outputPtr = output)
        fixed (UInt32* roundKeysPtr = roundKeys)
        {
            UInt32 x = *(UInt32*)ciphertextPtr;
            UInt32 y = *(UInt32*)(ciphertextPtr + 4);

            DecryptBlock(ref x, ref y, roundKeysPtr);

            *(UInt32*)outputPtr = x;
            *(UInt32*)(outputPtr + 4) = y;
        }
    }

    #endregion Public Methods

    #region Private Methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static UInt32[] ExpandKey(Byte[] key) => ExpandKey((ReadOnlySpan<Byte>)key);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static UInt32[] ExpandKey(ReadOnlySpan<Byte> key)
    {
        UInt32[] roundKeys = new UInt32[ROUNDS];

        fixed (Byte* keyPtr = key)
        fixed (UInt32* roundKeysPtr = roundKeys)
        {
            UInt32 keyA = *(UInt32*)keyPtr;
            UInt32 keyB = *(UInt32*)(keyPtr + 4);
            UInt32 keyC = *(UInt32*)(keyPtr + 8);
            UInt32 keyD = *(UInt32*)(keyPtr + 12);

            roundKeysPtr[0] = keyA;

            for (Int32 i = 0; i < ROUNDS - 1; i++)
            {
                keyB = BitwiseUtils.RotateRight(keyB, 8);
                keyB = BitwiseUtils.Add(keyB, keyA);
                keyB = BitwiseUtils.XOr(keyB, (UInt32)i);
                keyA = BitwiseUtils.RotateLeft(keyA, 3);
                keyA = BitwiseUtils.XOr(keyA, keyB);

                roundKeysPtr[i + 1] = keyA;

                if ((i + 1) % 3 == 0)
                {
                    UInt32 temp = keyA;
                    keyA = keyB;
                    keyB = keyC;
                    keyC = keyD;
                    keyD = temp;
                }
            }
        }

        return roundKeys;
    }

    /// <summary>
    /// Encrypts a 64-bit block.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EncryptBlock(ref UInt32 x, ref UInt32 y, UInt32* roundKeys)
    {
        for (Int32 i = 0; i < ROUNDS; i++)
        {
            // Apply ARX (Add-Rotate-XOR) operations for each round
            x = BitwiseUtils.RotateRight(x, 8);
            x = BitwiseUtils.Add(x, y);
            x = BitwiseUtils.XOr(x, roundKeys[i]);
            y = BitwiseUtils.RotateLeft(y, 3);
            y = BitwiseUtils.XOr(y, x);
        }
    }

    /// <summary>
    /// Decrypts a 64-bit block.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DecryptBlock(ref UInt32 x, ref UInt32 y, UInt32* roundKeys)
    {
        for (Int32 i = ROUNDS - 1; i >= 0; i--)
        {
            // Reverse the encryption operations
            y = BitwiseUtils.XOr(y, x);
            y = BitwiseUtils.RotateRight(y, 3);
            x = BitwiseUtils.XOr(x, roundKeys[i]);
            x = BitwiseUtils.Subtract(x, y);
            x = BitwiseUtils.RotateLeft(x, 8);
        }
    }

    #endregion Private Methods
}
