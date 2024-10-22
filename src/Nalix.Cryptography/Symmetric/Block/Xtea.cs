namespace Nalix.Cryptography.Symmetric.Block;

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
    public const System.Byte KeySize = 16;

    /// <summary>
    /// Block size in bytes (64 bits)
    /// </summary>
    public const System.Byte BlockSize = 8;

    /// <summary>
    /// Default number of rounds in XTEA algorithm
    /// </summary>
    public const System.Byte DefaultRounds = 64;

    /// <summary>
    /// XTEA delta constant
    /// </summary>
    private const System.UInt32 Delta = 0x9E3779B9;

    #endregion Constants

    #region Core Encryption/Decryption Methods

    /// <summary>
    /// Encrypts a single 64-bit block of data using XTEA algorithm
    /// </summary>
    /// <param name="v0">First 32-bit word</param>
    /// <param name="v1">Second 32-bit word</param>
    /// <param name="key">Pointer to 128-bit key (as 4 uint values)</param>
    /// <param name="rounds">ProtocolType of rounds (default is 32)</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void EncryptBlock(
        ref System.UInt32 v0,
        ref System.UInt32 v1,
        System.UInt32* key,
        System.Byte rounds = DefaultRounds)
    {
        System.UInt32 sum = 0;

        for (System.Byte i = 0; i < rounds; i++)
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
    /// <param name="rounds">ProtocolType of rounds (default is 32)</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void DecryptBlock(
        ref System.UInt32 v0,
        ref System.UInt32 v1,
        System.UInt32* key,
        System.Byte rounds = DefaultRounds)
    {
        System.UInt32 sum = rounds * Delta;

        for (System.Byte i = 0; i < rounds; i++)
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
    public static System.Byte[] Encrypt(
        System.ReadOnlySpan<System.Byte> plaintext,
        System.ReadOnlySpan<System.Byte> key,
        System.Byte rounds = DefaultRounds)
    {
        AssertInputSizes(plaintext, key);

        // rent a buffer (thuê bộ đệm) at least plaintext.Length
        System.Byte[] rented = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(plaintext.Length);
        try
        {
            System.Int32 written = Encrypt(
                plaintext, key, System.MemoryExtensions.AsSpan(rented, 0, plaintext.Length), rounds);

            // copy out only the used portion
            System.Byte[] result = new System.Byte[written];
            System.Buffer.BlockCopy(rented, 0, result, 0, written);
            return result;
        }
        finally
        {
            System.Buffers.ArrayPool<System.Byte>.Shared.Return(rented);
        }
    }

    /// <summary>
    /// Decrypts data using XTEA algorithm
    /// </summary>
    /// <param name="ciphertext">Data to decrypt (must be multiple of 8 bytes)</param>
    /// <param name="key">128-bit key (16 bytes)</param>
    /// <param name="rounds">ProtocolType of rounds (default is 32)</param>
    /// <returns>Decrypted data</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Byte[] Decrypt(
        System.ReadOnlySpan<System.Byte> ciphertext,
        System.ReadOnlySpan<System.Byte> key,
        System.Byte rounds = DefaultRounds)
    {
        AssertInputSizes(ciphertext, key);

        System.Byte[] plaintext = new System.Byte[ciphertext.Length];
        fixed (System.Byte* keyPtr = key)
        fixed (System.Byte* ciphertextPtr = ciphertext)
        fixed (System.Byte* plaintextPtr = plaintext)
        {
            System.UInt32* keyWords = (System.UInt32*)keyPtr;

            // Process each 8-byte block
            for (System.UInt32 i = 0; i < ciphertext.Length; i += BlockSize)
            {
                // Get pointers to current block
                System.UInt32* inputBlock = (System.UInt32*)(ciphertextPtr + i);
                System.UInt32* outputBlock = (System.UInt32*)(plaintextPtr + i);

                // Copy input values
                System.UInt32 v0 = inputBlock[0];
                System.UInt32 v1 = inputBlock[1];

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
    /// <param name="rounds">ProtocolType of rounds (default is 32)</param>
    /// <returns>ProtocolType of bytes written</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Int32 Encrypt(
        System.ReadOnlySpan<System.Byte> plaintext,
        System.ReadOnlySpan<System.Byte> key,
        System.Span<System.Byte> output,
        System.Byte rounds = DefaultRounds)
    {
        AssertInputSizes(plaintext, key);

        if (output.Length < plaintext.Length)
        {
            throw new System.ArgumentException("Output buffer too small", nameof(output));
        }

        fixed (System.Byte* keyPtr = key)
        fixed (System.Byte* plaintextPtr = plaintext)
        fixed (System.Byte* outputPtr = output)
        {
            System.UInt32* keyWords = (System.UInt32*)keyPtr;

            // Process each 8-byte block
            for (System.UInt32 i = 0; i < plaintext.Length; i += BlockSize)
            {
                // Get pointers to current block
                System.UInt32* inputBlock = (System.UInt32*)(plaintextPtr + i);
                System.UInt32* outputBlock = (System.UInt32*)(outputPtr + i);

                // Copy input values
                System.UInt32 v0 = inputBlock[0];
                System.UInt32 v1 = inputBlock[1];

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
    /// <param name="rounds">ProtocolType of rounds (default is 32)</param>
    /// <returns>ProtocolType of bytes written</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Int32 Decrypt(
        System.ReadOnlySpan<System.Byte> ciphertext,
        System.ReadOnlySpan<System.Byte> key,
        System.Span<System.Byte> output,
        System.Byte rounds = DefaultRounds)
    {
        AssertInputSizes(ciphertext, key);

        if (output.Length < ciphertext.Length)
        {
            throw new System.ArgumentException("Output buffer too small", nameof(output));
        }

        fixed (System.Byte* keyPtr = key)
        fixed (System.Byte* ciphertextPtr = ciphertext)
        fixed (System.Byte* outputPtr = output)
        {
            System.UInt32* keyWords = (System.UInt32*)keyPtr;

            // Process each 8-byte block
            for (System.Int32 i = 0; i < ciphertext.Length; i += BlockSize)
            {
                // Get pointers to current block
                System.UInt32* inputBlock = (System.UInt32*)(ciphertextPtr + i);
                System.UInt32* outputBlock = (System.UInt32*)(outputPtr + i);

                // Copy input values
                System.UInt32 v0 = inputBlock[0];
                System.UInt32 v1 = inputBlock[1];

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
    private static void AssertInputSizes(
        System.ReadOnlySpan<System.Byte> data,
        System.ReadOnlySpan<System.Byte> key)
    {
        if (data.Length % BlockSize != 0)
        {
            throw new System.ArgumentException(
                $"Data length must be a multiple of {BlockSize} bytes", nameof(data));
        }

        if (key.Length != KeySize)
        {
            throw new System.ArgumentException(
                $"Key must be exactly {KeySize} bytes", nameof(key));
        }
    }

    #endregion Helper Methods
}