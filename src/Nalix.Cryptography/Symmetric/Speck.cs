using Nalix.Cryptography.Utils;
using System;
using System.Runtime.CompilerServices;

namespace Nalix.Cryptography.Symmetric;

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
    private const int ROUNDS = 27;

    private const int BLOCK_SIZE_BYTES = 8;  // 64 bits = 8 bytes
    private const int KEY_SIZE_BYTES = 16;   // 128 bits = 16 bytes

    #endregion Constants

    #region Public Methods

    /// <summary>
    /// Encrypts a single 8-byte (64-bit) block using the Speck cipher.
    /// </summary>
    /// <param name="plaintext">The input data to encrypt (8 bytes).</param>
    /// <param name="key">The encryption key (16 bytes).</param>
    /// <returns>The encrypted ciphertext (8 bytes).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] Encrypt(byte[] plaintext, byte[] key)
    {
        if (plaintext.Length != BLOCK_SIZE_BYTES)
            throw new ArgumentException($"Plaintext must be exactly {BLOCK_SIZE_BYTES} bytes.", nameof(plaintext));
        if (key.Length != KEY_SIZE_BYTES)
            throw new ArgumentException($"Key must be exactly {KEY_SIZE_BYTES} bytes.", nameof(key));

        uint[] roundKeys = ExpandKey(key);
        byte[] ciphertext = new byte[BLOCK_SIZE_BYTES];

        fixed (byte* plaintextPtr = plaintext)
        fixed (byte* ciphertextPtr = ciphertext)
        fixed (uint* roundKeysPtr = roundKeys)
        {
            // Load plaintext into two 32-bit halves
            uint x = *(uint*)plaintextPtr;
            uint y = *(uint*)(plaintextPtr + 4);

            // Encrypt
            EncryptBlock(ref x, ref y, roundKeysPtr);

            // Store the result back into bytes
            *(uint*)ciphertextPtr = x;
            *(uint*)(ciphertextPtr + 4) = y;
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
    public static byte[] Decrypt(byte[] ciphertext, byte[] key)
    {
        if (ciphertext.Length != BLOCK_SIZE_BYTES)
            throw new ArgumentException($"Ciphertext must be exactly {BLOCK_SIZE_BYTES} bytes.", nameof(ciphertext));
        if (key.Length != KEY_SIZE_BYTES)
            throw new ArgumentException($"Key must be exactly {KEY_SIZE_BYTES} bytes.", nameof(key));

        uint[] roundKeys = ExpandKey(key);
        byte[] plaintext = new byte[BLOCK_SIZE_BYTES];

        fixed (byte* ciphertextPtr = ciphertext)
        fixed (byte* plaintextPtr = plaintext)
        fixed (uint* roundKeysPtr = roundKeys)
        {
            // Load ciphertext into two 32-bit halves
            uint x = *(uint*)ciphertextPtr;
            uint y = *(uint*)(ciphertextPtr + 4);

            // Decrypt
            DecryptBlock(ref x, ref y, roundKeysPtr);

            // Store the result back into bytes
            *(uint*)plaintextPtr = x;
            *(uint*)(plaintextPtr + 4) = y;
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
    public static void Encrypt(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> key, Span<byte> output)
    {
        if (plaintext.Length != BLOCK_SIZE_BYTES)
            throw new ArgumentException($"Plaintext must be exactly {BLOCK_SIZE_BYTES} bytes.", nameof(plaintext));
        if (key.Length != KEY_SIZE_BYTES)
            throw new ArgumentException($"Key must be exactly {KEY_SIZE_BYTES} bytes.", nameof(key));
        if (output.Length != BLOCK_SIZE_BYTES)
            throw new ArgumentException($"Output must be exactly {BLOCK_SIZE_BYTES} bytes.", nameof(output));

        uint[] roundKeys = ExpandKey(key);

        fixed (byte* plaintextPtr = plaintext)
        fixed (byte* outputPtr = output)
        fixed (uint* roundKeysPtr = roundKeys)
        {
            uint x = *(uint*)plaintextPtr;
            uint y = *(uint*)(plaintextPtr + 4);

            EncryptBlock(ref x, ref y, roundKeysPtr);

            *(uint*)outputPtr = x;
            *(uint*)(outputPtr + 4) = y;
        }
    }

    /// <summary>
    /// Decrypts a 64-bit block using Span input and output.
    /// </summary>
    /// <param name="ciphertext">The encrypted input data (8 bytes).</param>
    /// <param name="key">The decryption key (16 bytes).</param>
    /// <param name="output">The destination span to write the plaintext.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Decrypt(ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> key, Span<byte> output)
    {
        if (ciphertext.Length != BLOCK_SIZE_BYTES)
            throw new ArgumentException($"Ciphertext must be exactly {BLOCK_SIZE_BYTES} bytes.", nameof(ciphertext));
        if (key.Length != KEY_SIZE_BYTES)
            throw new ArgumentException($"Key must be exactly {KEY_SIZE_BYTES} bytes.", nameof(key));
        if (output.Length != BLOCK_SIZE_BYTES)
            throw new ArgumentException($"Output must be exactly {BLOCK_SIZE_BYTES} bytes.", nameof(output));

        uint[] roundKeys = ExpandKey(key);

        fixed (byte* ciphertextPtr = ciphertext)
        fixed (byte* outputPtr = output)
        fixed (uint* roundKeysPtr = roundKeys)
        {
            uint x = *(uint*)ciphertextPtr;
            uint y = *(uint*)(ciphertextPtr + 4);

            DecryptBlock(ref x, ref y, roundKeysPtr);

            *(uint*)outputPtr = x;
            *(uint*)(outputPtr + 4) = y;
        }
    }

    #endregion Public Methods

    #region Private Methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint[] ExpandKey(byte[] key) => ExpandKey((ReadOnlySpan<byte>)key);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint[] ExpandKey(ReadOnlySpan<byte> key)
    {
        uint[] roundKeys = new uint[ROUNDS];

        fixed (byte* keyPtr = key)
        fixed (uint* roundKeysPtr = roundKeys)
        {
            uint keyA = *(uint*)keyPtr;
            uint keyB = *(uint*)(keyPtr + 4);
            uint keyC = *(uint*)(keyPtr + 8);
            uint keyD = *(uint*)(keyPtr + 12);

            roundKeysPtr[0] = keyA;

            for (int i = 0; i < ROUNDS - 1; i++)
            {
                keyB = BitwiseUtils.RotateRight(keyB, 8);
                keyB = BitwiseUtils.Add(keyB, keyA);
                keyB = BitwiseUtils.XOr(keyB, (uint)i);
                keyA = BitwiseUtils.RotateLeft(keyA, 3);
                keyA = BitwiseUtils.XOr(keyA, keyB);

                roundKeysPtr[i + 1] = keyA;

                if ((i + 1) % 3 == 0)
                {
                    uint temp = keyA;
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
    private static void EncryptBlock(ref uint x, ref uint y, uint* roundKeys)
    {
        for (int i = 0; i < ROUNDS; i++)
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
    private static void DecryptBlock(ref uint x, ref uint y, uint* roundKeys)
    {
        for (int i = ROUNDS - 1; i >= 0; i--)
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
