using System;
using System.Runtime.CompilerServices;

namespace Nalix.Cryptography.Symmetric;

/// <summary>
/// Provides an implementation of XTEA (eXtended Tiny Encryption Algorithm) cipher
/// using unsafe code for performance.
/// </summary>
/// <remarks>
/// XTEA is a block cipher with 64-bit blocks and 128-bit keys.
/// This implementation uses direct memory manipulation for enhanced performance.
/// </remarks>
public static unsafe class Xtea
{
    #region Constants

    /// <summary>
    /// Key size in bytes (128 bits)
    /// </summary>
    public const int KeySize = 16;

    /// <summary>
    /// Block size in bytes (64 bits)
    /// </summary>
    public const int BlockSize = 8;

    /// <summary>
    /// Default number of rounds in XTEA algorithm
    /// </summary>
    public const int DefaultRounds = 32;

    /// <summary>
    /// XTEA delta constant
    /// </summary>
    private const uint Delta = 0x9E3779B9;

    #endregion Constants

    #region Fields

    private static readonly System.Buffers.ArrayPool<byte> Pool = System.Buffers.ArrayPool<byte>.Shared;

    #endregion Fields

    #region Core Encryption/Decryption Methods

    /// <summary>
    /// Encrypts a single 64-bit block of data using XTEA algorithm
    /// </summary>
    /// <param name="v0">First 32-bit word</param>
    /// <param name="v1">Second 32-bit word</param>
    /// <param name="key">Pointer to 128-bit key (as 4 uint values)</param>
    /// <param name="rounds">Number of rounds (default is 32)</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EncryptBlock(ref uint v0, ref uint v1, uint* key, int rounds = DefaultRounds)
    {
        uint sum = 0;

        for (int i = 0; i < rounds; i++)
        {
            v0 += (((v1 << 4) ^ (v1 >> 5)) + v1) ^ (sum + key[sum & 3]);
            sum += Delta;
            v1 += (((v0 << 4) ^ (v0 >> 5)) + v0) ^ (sum + key[(sum >> 11) & 3]);
        }
    }

    /// <summary>
    /// Decrypts a single 64-bit block of data using XTEA algorithm
    /// </summary>
    /// <param name="v0">First 32-bit word</param>
    /// <param name="v1">Second 32-bit word</param>
    /// <param name="key">Pointer to 128-bit key (as 4 uint values)</param>
    /// <param name="rounds">Number of rounds (default is 32)</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DecryptBlock(ref uint v0, ref uint v1, uint* key, int rounds = DefaultRounds)
    {
        uint sum = (uint)rounds * Delta;

        for (int i = 0; i < rounds; i++)
        {
            v1 -= (((v0 << 4) ^ (v0 >> 5)) + v0) ^ (sum + key[(sum >> 11) & 3]);
            sum -= Delta;
            v0 -= (((v1 << 4) ^ (v1 >> 5)) + v1) ^ (sum + key[sum & 3]);
        }
    }

    #endregion Core Encryption/Decryption Methods

    #region Public API Methods

    /// <summary>
    /// Encrypts data using the XTEA algorithm.
    /// </summary>
    /// <param name="plaintext">The data to encrypt. Must be a multiple of 8 bytes.</param>
    /// <param name="key">The 128-bit encryption key (16 bytes).</param>
    /// <param name="rounds">The number of encryption rounds. Default is 32.</param>
    /// <returns>The encrypted data as a byte array.</returns>
    public static byte[] Encrypt(
        ReadOnlySpan<byte> plaintext,
        ReadOnlySpan<byte> key,
        int rounds = Xtea.DefaultRounds)
    {
        AssertInputSizes(plaintext, key);

        // rent a buffer (thuê bộ đệm) at least plaintext.Length
        byte[] rented = Pool.Rent(plaintext.Length);
        try
        {
            int written = Encrypt(plaintext, key, rented.AsSpan(0, plaintext.Length), rounds);
            // copy out only the used portion
            var result = new byte[written];
            Buffer.BlockCopy(rented, 0, result, 0, written);
            return result;
        }
        finally
        {
            Pool.Return(rented);
        }
    }

    /// <summary>
    /// Decrypts data using XTEA algorithm
    /// </summary>
    /// <param name="ciphertext">Data to decrypt (must be multiple of 8 bytes)</param>
    /// <param name="key">128-bit key (16 bytes)</param>
    /// <param name="rounds">Number of rounds (default is 32)</param>
    /// <returns>Decrypted data</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static byte[] Decrypt(
        ReadOnlySpan<byte> ciphertext,
        ReadOnlySpan<byte> key,
        int rounds = DefaultRounds)
    {
        AssertInputSizes(ciphertext, key);

        byte[] plaintext = new byte[ciphertext.Length];
        fixed (byte* keyPtr = key)
        fixed (byte* ciphertextPtr = ciphertext)
        fixed (byte* plaintextPtr = plaintext)
        {
            uint* keyWords = (uint*)keyPtr;

            // Process each 8-byte block
            for (int i = 0; i < ciphertext.Length; i += BlockSize)
            {
                // Get pointers to current block
                uint* inputBlock = (uint*)(ciphertextPtr + i);
                uint* outputBlock = (uint*)(plaintextPtr + i);

                // Copy input values
                uint v0 = inputBlock[0];
                uint v1 = inputBlock[1];

                // Decrypt the block
                DecryptBlock(ref v0, ref v1, keyWords, rounds);

                // Store results
                outputBlock[0] = v0;
                outputBlock[1] = v1;
            }
        }

        return plaintext;
    }

    /// <summary>
    /// Encrypts data using XTEA algorithm and writes the result to a buffer
    /// </summary>
    /// <param name="plaintext">Data to encrypt (must be multiple of 8 bytes)</param>
    /// <param name="key">128-bit key (16 bytes)</param>
    /// <param name="output">Output buffer (must be at least as large as plaintext)</param>
    /// <param name="rounds">Number of rounds (default is 32)</param>
    /// <returns>Number of bytes written</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Encrypt(
        ReadOnlySpan<byte> plaintext,
        ReadOnlySpan<byte> key,
        Span<byte> output, int rounds = DefaultRounds)
    {
        AssertInputSizes(plaintext, key);

        if (output.Length < plaintext.Length)
            throw new ArgumentException("Output buffer too small", nameof(output));

        fixed (byte* keyPtr = key)
        fixed (byte* plaintextPtr = plaintext)
        fixed (byte* outputPtr = output)
        {
            uint* keyWords = (uint*)keyPtr;

            // Process each 8-byte block
            for (int i = 0; i < plaintext.Length; i += BlockSize)
            {
                // Get pointers to current block
                uint* inputBlock = (uint*)(plaintextPtr + i);
                uint* outputBlock = (uint*)(outputPtr + i);

                // Copy input values
                uint v0 = inputBlock[0];
                uint v1 = inputBlock[1];

                // Encrypt the block
                EncryptBlock(ref v0, ref v1, keyWords, rounds);

                // Store results
                outputBlock[0] = v0;
                outputBlock[1] = v1;
            }
        }

        return plaintext.Length;
    }

    /// <summary>
    /// Decrypts data using XTEA algorithm and writes the result to a buffer
    /// </summary>
    /// <param name="ciphertext">Data to decrypt (must be multiple of 8 bytes)</param>
    /// <param name="key">128-bit key (16 bytes)</param>
    /// <param name="output">Output buffer (must be at least as large as ciphertext)</param>
    /// <param name="rounds">Number of rounds (default is 32)</param>
    /// <returns>Number of bytes written</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Decrypt(
        ReadOnlySpan<byte> ciphertext,
        ReadOnlySpan<byte> key,
        Span<byte> output,
        int rounds = DefaultRounds)
    {
        AssertInputSizes(ciphertext, key);

        if (output.Length < ciphertext.Length)
            throw new ArgumentException("Output buffer too small", nameof(output));

        fixed (byte* keyPtr = key)
        fixed (byte* ciphertextPtr = ciphertext)
        fixed (byte* outputPtr = output)
        {
            uint* keyWords = (uint*)keyPtr;

            // Process each 8-byte block
            for (int i = 0; i < ciphertext.Length; i += BlockSize)
            {
                // Get pointers to current block
                uint* inputBlock = (uint*)(ciphertextPtr + i);
                uint* outputBlock = (uint*)(outputPtr + i);

                // Copy input values
                uint v0 = inputBlock[0];
                uint v1 = inputBlock[1];

                // Decrypt the block
                DecryptBlock(ref v0, ref v1, keyWords, rounds);

                // Store results
                outputBlock[0] = v0;
                outputBlock[1] = v1;
            }
        }

        return ciphertext.Length;
    }

    #endregion Public API Methods

    #region Helper Methods

    /// <summary>
    /// Validates input parameters
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void AssertInputSizes(ReadOnlySpan<byte> data, ReadOnlySpan<byte> key)
    {
        if (data.Length % BlockSize != 0)
            throw new ArgumentException($"Data length must be a multiple of {BlockSize} bytes", nameof(data));

        if (key.Length != KeySize)
            throw new ArgumentException($"Key must be exactly {KeySize} bytes", nameof(key));
    }

    #endregion Helper Methods
}