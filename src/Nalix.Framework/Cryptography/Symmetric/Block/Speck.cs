// Copyright (c) 2025 PPN Corporation. All rights reserved.


// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Cryptography.Primitives;

namespace Nalix.Framework.Cryptography.Symmetric.Block;

/// <summary>
/// Implementation of the Speck lightweight block cipher developed by the NSA.
/// This implementation does not rely on system cryptographic libraries for its core algorithm,
/// but uses a secure random number generator for IV generation when necessary.
/// </summary>
[System.Runtime.CompilerServices.SkipLocalsInit]
public static unsafe class Speck
{
    #region Constants

    // Speck 64/128 — 64-bit block with a 128-bit key
    private const System.Int32 ROUNDS = 27;

    private const System.Int32 BLOCK_SIZE_BYTES = 8;  // 64 bits = 8 bytes
    private const System.Int32 KEY_SIZE_BYTES = 16;   // 128 bits = 16 bytes

    #endregion Constants

    #region Public Methods

    /// <summary>
    /// Encrypts a 64-bit block using Span input and output.
    /// </summary>
    /// <param name="plaintext">The input data (8 bytes).</param>
    /// <param name="key">The encryption key (16 bytes).</param>
    /// <param name="output">The destination span to write the ciphertext.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Encrypt(
        System.ReadOnlySpan<System.Byte> plaintext,
        System.ReadOnlySpan<System.Byte> key,
        System.Span<System.Byte> output)
    {
        if (plaintext.Length != BLOCK_SIZE_BYTES)
        {
            throw new System.ArgumentException($"Plaintext must be exactly {BLOCK_SIZE_BYTES} bytes.", nameof(plaintext));
        }

        if (key.Length != KEY_SIZE_BYTES)
        {
            throw new System.ArgumentException($"Key must be exactly {KEY_SIZE_BYTES} bytes.", nameof(key));
        }

        if (output.Length != BLOCK_SIZE_BYTES)
        {
            throw new System.ArgumentException($"Output must be exactly {BLOCK_SIZE_BYTES} bytes.", nameof(output));
        }

        System.UInt32[] roundKeys = ExpandKey(key);

        fixed (System.Byte* plaintextPtr = plaintext)
        {
            fixed (System.Byte* outputPtr = output)
            {
                fixed (System.UInt32* roundKeysPtr = roundKeys)
                {
                    System.UInt32 x = *(System.UInt32*)plaintextPtr;
                    System.UInt32 y = *(System.UInt32*)(plaintextPtr + 4);

                    EncryptBlock(ref x, ref y, roundKeysPtr);

                    *(System.UInt32*)outputPtr = x;
                    *(System.UInt32*)(outputPtr + 4) = y;
                }
            }
        }
    }

    /// <summary>
    /// Decrypts a 64-bit block using Span input and output.
    /// </summary>
    /// <param name="ciphertext">The encrypted input data (8 bytes).</param>
    /// <param name="key">The decryption key (16 bytes).</param>
    /// <param name="output">The destination span to write the plaintext.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Decrypt(
        System.ReadOnlySpan<System.Byte> ciphertext,
        System.ReadOnlySpan<System.Byte> key,
        System.Span<System.Byte> output)
    {
        if (ciphertext.Length != BLOCK_SIZE_BYTES)
        {
            throw new System.ArgumentException($"Ciphertext must be exactly {BLOCK_SIZE_BYTES} bytes.", nameof(ciphertext));
        }

        if (key.Length != KEY_SIZE_BYTES)
        {
            throw new System.ArgumentException($"Key must be exactly {KEY_SIZE_BYTES} bytes.", nameof(key));
        }

        if (output.Length != BLOCK_SIZE_BYTES)
        {
            throw new System.ArgumentException($"Output must be exactly {BLOCK_SIZE_BYTES} bytes.", nameof(output));
        }

        System.UInt32[] roundKeys = ExpandKey(key);

        fixed (System.Byte* ciphertextPtr = ciphertext)
        {
            fixed (System.Byte* outputPtr = output)
            {
                fixed (System.UInt32* roundKeysPtr = roundKeys)
                {
                    System.UInt32 x = *(System.UInt32*)ciphertextPtr;
                    System.UInt32 y = *(System.UInt32*)(ciphertextPtr + 4);

                    DecryptBlock(ref x, ref y, roundKeysPtr);

                    *(System.UInt32*)outputPtr = x;
                    *(System.UInt32*)(outputPtr + 4) = y;
                }
            }
        }
    }

    #endregion Public Methods

    #region Private Methods

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.UInt32[] ExpandKey(System.Byte[] key) => ExpandKey((System.ReadOnlySpan<System.Byte>)key);

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.UInt32[] ExpandKey(System.ReadOnlySpan<System.Byte> key)
    {
        System.UInt32[] roundKeys = new System.UInt32[ROUNDS];

        fixed (System.Byte* keyPtr = key)
        {
            fixed (System.UInt32* roundKeysPtr = roundKeys)
            {
                System.UInt32 keyA = *(System.UInt32*)keyPtr;
                System.UInt32 keyB = *(System.UInt32*)(keyPtr + 4);
                System.UInt32 keyC = *(System.UInt32*)(keyPtr + 8);
                System.UInt32 keyD = *(System.UInt32*)(keyPtr + 12);

                roundKeysPtr[0] = keyA;

                for (System.Int32 i = 0; i < ROUNDS - 1; i++)
                {
                    keyB = BitwiseOperations.RotateRight(keyB, 8);
                    keyB = BitwiseOperations.Add(keyB, keyA);
                    keyB = BitwiseOperations.XOr(keyB, (System.UInt32)i);
                    keyA = BitwiseOperations.RotateLeft(keyA, 3);
                    keyA = BitwiseOperations.XOr(keyA, keyB);

                    roundKeysPtr[i + 1] = keyA;

                    if ((i + 1) % 3 == 0)
                    {
                        System.UInt32 temp = keyA;
                        keyA = keyB;
                        keyB = keyC;
                        keyC = keyD;
                        keyD = temp;
                    }
                }
            }
        }

        return roundKeys;
    }

    /// <summary>
    /// Encrypts a 64-bit block.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void EncryptBlock(
        ref System.UInt32 x,
        ref System.UInt32 y,
        System.UInt32* roundKeys)
    {
        for (System.Int32 i = 0; i < ROUNDS; i++)
        {
            // Apply ARX (Push-Rotate-XOR) operations for each round
            x = BitwiseOperations.RotateRight(x, 8);
            x = BitwiseOperations.Add(x, y);
            x = BitwiseOperations.XOr(x, roundKeys[i]);
            y = BitwiseOperations.RotateLeft(y, 3);
            y = BitwiseOperations.XOr(y, x);
        }
    }

    /// <summary>
    /// Decrypts a 64-bit block.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void DecryptBlock(
        ref System.UInt32 x,
        ref System.UInt32 y,
        System.UInt32* roundKeys)
    {
        for (System.Int32 i = ROUNDS - 1; i >= 0; i--)
        {
            // Reverse the encryption operations
            y = BitwiseOperations.XOr(y, x);
            y = BitwiseOperations.RotateRight(y, 3);
            x = BitwiseOperations.XOr(x, roundKeys[i]);
            x = BitwiseOperations.Subtract(x, y);
            x = BitwiseOperations.RotateLeft(x, 8);
        }
    }

    #endregion Private Methods
}
