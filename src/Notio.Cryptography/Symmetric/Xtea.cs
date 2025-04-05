using Notio.Common.Exceptions;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace Notio.Cryptography.Symmetric;

/// <summary>
/// Provides high-performance methods for encrypting and decrypting data using the XTEA 
/// (eXtended Tiny Encryption Algorithm) with enhanced security features.
/// </summary>
/// <remarks>
/// XTEA is a block cipher designed to correct weaknesses in TEA. It uses a 128-bit key and 
/// operates on 64-bit blocks. This implementation includes additional security features
/// like initialization vector support and secure padding modes.
/// </remarks>
public static class Xtea
{
    #region Constants

    /// <summary>
    /// The Number of rounds in the XTEA algorithm.
    /// </summary>
    public const int DefaultNumRounds = 32;

    /// <summary>
    /// The block size in bytes (XTEA operates on 64-bit blocks).
    /// </summary>
    public const int BlockSizeInBytes = 8;

    /// <summary>
    /// The key size in bytes (XTEA uses a 128-bit key).
    /// </summary>
    public const int KeySizeInBytes = 16;

    /// <summary>
    /// The key size in 32-bit words (XTEA uses four 32-bit words for the key).
    /// </summary>
    public const int KeySizeInWords = 4;

    /// <summary>
    /// The XTEA delta constant (derived from the golden ratio).
    /// </summary>
    private const uint Delta = 0x9E3779B9;

    #endregion

    #region Encryption/Decryption Core

    /// <summary>
    /// Encrypts a single 64-bit block using the XTEA algorithm.
    /// </summary>
    /// <param name="v0">The first 32 bits of the block.</param>
    /// <param name="v1">The second 32 bits of the block.</param>
    /// <param name="key">The 128-bit key as four 32-bit unsigned integers.</param>
    /// <param name="rounds">The Number of rounds (default is 32).</param>
    /// <returns>The encrypted 64-bit block as two 32-bit values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (uint v0, uint v1) EncryptBlock(
        uint v0, uint v1, ReadOnlySpan<uint> key, int rounds = DefaultNumRounds)
    {
        uint sum = 0;

        for (int i = 0; i < rounds; i++)
        {
            v0 += (((v1 << 4) ^ (v1 >> 5)) + v1) ^ (sum + key[(int)(sum & 3)]);
            sum += Delta;
            v1 += (((v0 << 4) ^ (v0 >> 5)) + v0) ^ (sum + key[(int)((sum >> 11) & 3)]);
        }

        return (v0, v1);
    }

    /// <summary>
    /// Decrypts a single 64-bit block using the XTEA algorithm.
    /// </summary>
    /// <param name="v0">The first 32 bits of the encrypted block.</param>
    /// <param name="v1">The second 32 bits of the encrypted block.</param>
    /// <param name="key">The 128-bit key as four 32-bit unsigned integers.</param>
    /// <param name="rounds">The Number of rounds (default is 32).</param>
    /// <returns>The decrypted 64-bit block as two 32-bit values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (uint v0, uint v1) DecryptBlock(
        uint v0, uint v1, ReadOnlySpan<uint> key, int rounds = DefaultNumRounds)
    {
        uint sum = unchecked(Delta * (uint)rounds);

        for (int i = 0; i < rounds; i++)
        {
            v1 -= (((v0 << 4) ^ (v0 >> 5)) + v0) ^ (sum + key[(int)((sum >> 11) & 3)]);
            sum -= Delta;
            v0 -= (((v1 << 4) ^ (v1 >> 5)) + v1) ^ (sum + key[(int)(sum & 3)]);
        }

        return (v0, v1);
    }

    #endregion

    #region Enhanced API Methods

    /// <summary>
    /// Encrypts the specified data using the XTEA algorithm with PKCS#7 padding.
    /// </summary>
    /// <param name="data">The data to encrypt.</param>
    /// <param name="key">The encryption key (must be exactly 16 bytes / 4 words).</param>
    /// <param name="output">The buffer to store the encrypted data (must be large enough to hold the result).</param>
    /// <param name="iv">Optional initialization vector for CBC mode. If null, ECB mode is used.</param>
    /// <param name="rounds">The Number of rounds to use (default is 32).</param>
    /// <exception cref="ArgumentException">
    /// Thrown when the data is empty, the key is not exactly 4 words,
    /// or the output buffer is too small.
    /// </exception>
    public static void Encrypt(
        ReadOnlySpan<byte> data,
        ReadOnlySpan<uint> key,
        Span<byte> output,
        ReadOnlySpan<byte> iv = default,
        int rounds = DefaultNumRounds)
    {
        // Validate parameters
        if (data.IsEmpty)
            throw new ArgumentException("Data cannot be empty", nameof(data));

        if (key.Length != KeySizeInWords)
            throw new ArgumentException($"Key must be exactly {KeySizeInWords} words ({KeySizeInBytes} bytes)", nameof(key));

        // Calculate the padded length
        int length = data.Length;
        int paddingBytes = BlockSizeInBytes - (length % BlockSizeInBytes);
        int paddedLength = length + paddingBytes;

        if (output.Length < paddedLength)
            throw new ArgumentException($"Output buffer must be at least {paddedLength} bytes", nameof(output));

        // Create a working buffer for the data with padding
        byte[] rentedArray = null;

        Span<byte> workingBuffer = paddedLength <= 512
            ? stackalloc byte[paddedLength]
            : (rentedArray = ArrayPool<byte>.Shared.Rent(paddedLength)).AsSpan(0, paddedLength);

        try
        {
            // Copy the data and apply PKCS#7 padding
            data.CopyTo(workingBuffer);
            for (int i = length; i < paddedLength; i++)
            {
                workingBuffer[i] = (byte)paddingBytes;
            }

            // Process in CBC mode if IV is provided, otherwise ECB
            bool useCbc = !iv.IsEmpty;
            Span<byte> previousCipherBlock = stackalloc byte[BlockSizeInBytes];

            if (useCbc)
            {
                if (iv.Length < BlockSizeInBytes)
                    throw new ArgumentException($"IV must be at least {BlockSizeInBytes} bytes", nameof(iv));

                iv[..BlockSizeInBytes].CopyTo(previousCipherBlock);
            }

            // Process each block
            for (int offset = 0; offset < paddedLength; offset += BlockSizeInBytes)
            {
                Span<byte> blockBytes = workingBuffer.Slice(offset, BlockSizeInBytes);

                if (useCbc)
                {
                    // XOR with the previous cipher block (IV for the first block)
                    for (int i = 0; i < BlockSizeInBytes; i++)
                    {
                        blockBytes[i] ^= previousCipherBlock[i];
                    }
                }

                // Extract the block as two 32-bit words
                uint v0 = BinaryPrimitives.ReadUInt32LittleEndian(blockBytes[..4]);
                uint v1 = BinaryPrimitives.ReadUInt32LittleEndian(blockBytes.Slice(4, 4));

                // Encrypt the block
                (v0, v1) = EncryptBlock(v0, v1, key, rounds);

                // Write the encrypted block back
                BinaryPrimitives.WriteUInt32LittleEndian(blockBytes[..4], v0);
                BinaryPrimitives.WriteUInt32LittleEndian(blockBytes.Slice(4, 4), v1);

                if (useCbc)
                {
                    // Save this cipher block for the next iteration
                    blockBytes.CopyTo(previousCipherBlock);
                }

                // Copy to the output
                blockBytes.CopyTo(output.Slice(offset, BlockSizeInBytes));
            }
        }
        finally
        {
            // Return any rented array
            if (rentedArray != null)
            {
                ArrayPool<byte>.Shared.Return(rentedArray, clearArray: true);
            }
        }
    }

    /// <summary>
    /// Encrypts the specified data using the XTEA algorithm with PKCS#7 padding.
    /// </summary>
    /// <param name="data">The data to encrypt.</param>
    /// <param name="key">The encryption key (must be exactly 16 bytes / 4 words).</param>
    /// <param name="iv">Optional initialization vector for CBC mode. If null, ECB mode is used.</param>
    /// <param name="rounds">The Number of rounds to use (default is 32).</param>
    /// <returns>The encrypted data.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the data is empty or the key is not exactly 4 words.
    /// </exception>
    public static byte[] Encrypt(
        ReadOnlySpan<byte> data,
        ReadOnlySpan<uint> key,
        ReadOnlySpan<byte> iv = default,
        int rounds = DefaultNumRounds)
    {
        // Calculate the padded length
        int length = data.Length;
        int paddingBytes = BlockSizeInBytes - (length % BlockSizeInBytes);
        int paddedLength = length + paddingBytes;

        // Allocate output buffer
        byte[] output = new byte[paddedLength];
        Encrypt(data, key, output, iv, rounds);
        return output;
    }

    /// <summary>
    /// Encrypts the specified data using the XTEA algorithm with PKCS#7 padding.
    /// </summary>
    /// <param name="data">The data to encrypt.</param>
    /// <param name="key">The encryption key (must be exactly 16 bytes).</param>
    /// <param name="iv">Optional initialization vector for CBC mode. If null, ECB mode is used.</param>
    /// <param name="rounds">The Number of rounds to use (default is 32).</param>
    /// <returns>The encrypted data.</returns>
    public static byte[] Encrypt(
        ReadOnlySpan<byte> data,
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> iv = default,
        int rounds = DefaultNumRounds)
    {
        if (key.Length != KeySizeInBytes)
            throw new ArgumentException($"Key must be exactly {KeySizeInBytes} bytes", nameof(key));

        // Convert the key bytes to words
        Span<uint> keyWords = stackalloc uint[KeySizeInWords];
        for (int i = 0; i < KeySizeInWords; i++)
        {
            keyWords[i] = BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(i * 4, 4));
        }

        return Encrypt(data, keyWords, iv, rounds);
    }

    /// <summary>
    /// Decrypts the specified data using the XTEA algorithm, handling PKCS#7 padding.
    /// </summary>
    /// <param name="data">The data to decrypt (must be a multiple of 8 bytes).</param>
    /// <param name="key">The decryption key (must be exactly 16 bytes / 4 words).</param>
    /// <param name="output">The buffer to store the decrypted data.</param>
    /// <param name="iv">Optional initialization vector for CBC mode. If null, ECB mode is used.</param>
    /// <param name="rounds">The Number of rounds to use (default is 32).</param>
    /// <returns>The Number of bytes written to the output buffer after removing padding.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the key is not exactly 4 words, the data length is not a multiple of 8,
    /// the output buffer is too small, or if the padding is invalid.
    /// </exception>
    public static int Decrypt(
        ReadOnlySpan<byte> data,
        ReadOnlySpan<uint> key,
        Span<byte> output,
        ReadOnlySpan<byte> iv = default,
        int rounds = DefaultNumRounds)
    {
        // Validate parameters
        if (data.Length % BlockSizeInBytes != 0)
            throw new ArgumentException($"Data length must be a multiple of {BlockSizeInBytes} bytes", nameof(data));

        if (key.Length != KeySizeInWords)
            throw new ArgumentException($"Key must be exactly {KeySizeInWords} words ({KeySizeInBytes} bytes)", nameof(key));

        if (output.Length < data.Length)
            throw new ArgumentException("Output buffer is too small", nameof(output));

        // Create a working buffer for the decrypted data
        int dataLength = data.Length;
        byte[] rentedArray = null;

        Span<byte> workingBuffer = dataLength <= 512
            ? stackalloc byte[dataLength]
            : (rentedArray = ArrayPool<byte>.Shared.Rent(dataLength)).AsSpan(0, dataLength);

        try
        {
            // Process in CBC mode if IV is provided, otherwise ECB
            bool useCbc = !iv.IsEmpty;
            Span<byte> previousCipherBlock = stackalloc byte[BlockSizeInBytes];

            if (useCbc)
            {
                if (iv.Length < BlockSizeInBytes)
                    throw new ArgumentException($"IV must be at least {BlockSizeInBytes} bytes", nameof(iv));

                iv[..BlockSizeInBytes].CopyTo(previousCipherBlock);
            }

            // Process each block
            Span<byte> currentCipherBlock = useCbc ? stackalloc byte[BlockSizeInBytes] : [];

            for (int offset = 0; offset < dataLength; offset += BlockSizeInBytes)
            {
                ReadOnlySpan<byte> cipherBlock = data.Slice(offset, BlockSizeInBytes);
                Span<byte> plainBlock = workingBuffer.Slice(offset, BlockSizeInBytes);

                // Extract the block as two 32-bit words
                uint v0 = BinaryPrimitives.ReadUInt32LittleEndian(cipherBlock[..4]);
                uint v1 = BinaryPrimitives.ReadUInt32LittleEndian(cipherBlock.Slice(4, 4));

                if (useCbc)
                {
                    cipherBlock.CopyTo(currentCipherBlock);
                }

                // Decrypt the block
                (v0, v1) = DecryptBlock(v0, v1, key, rounds);

                // Write the decrypted block
                BinaryPrimitives.WriteUInt32LittleEndian(plainBlock[..4], v0);
                BinaryPrimitives.WriteUInt32LittleEndian(plainBlock.Slice(4, 4), v1);

                if (useCbc)
                {
                    // XOR với previousCipherBlock
                    for (int i = 0; i < BlockSizeInBytes; i++)
                    {
                        plainBlock[i] ^= previousCipherBlock[i];
                    }

                    // Cập nhật previousCipherBlock
                    currentCipherBlock.CopyTo(previousCipherBlock);
                }
            }


            // Handle PKCS#7 padding
            int paddingLength = workingBuffer[dataLength - 1];
            if (paddingLength < 1 || paddingLength > BlockSizeInBytes)
                throw new CryptoException("Invalid padding");

            // Verify that all padding bytes have the correct value
            for (int i = dataLength - paddingLength; i < dataLength; i++)
            {
                if (workingBuffer[i] != paddingLength)
                    throw new CryptoException("Invalid padding");
            }

            // Calculate the original data length
            int originalDataLength = dataLength - paddingLength;

            // Copy the decrypted data to the output buffer
            workingBuffer[..originalDataLength].CopyTo(output);

            return originalDataLength;
        }
        finally
        {
            // Return any rented array
            if (rentedArray != null)
            {
                ArrayPool<byte>.Shared.Return(rentedArray, clearArray: true);
            }
        }
    }

    /// <summary>
    /// Decrypts the specified data using the XTEA algorithm, handling PKCS#7 padding.
    /// </summary>
    /// <param name="data">The data to decrypt (must be a multiple of 8 bytes).</param>
    /// <param name="key">The decryption key (must be exactly 16 bytes / 4 words).</param>
    /// <param name="iv">Optional initialization vector for CBC mode. If null, ECB mode is used.</param>
    /// <param name="rounds">The Number of rounds to use (default is 32).</param>
    /// <returns>The decrypted data with padding removed.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the key is not exactly 4 words, the data length is not a multiple of 8,
    /// or if the padding is invalid.
    /// </exception>
    public static byte[] Decrypt(
        ReadOnlySpan<byte> data,
        ReadOnlySpan<uint> key,
        ReadOnlySpan<byte> iv = default,
        int rounds = DefaultNumRounds)
    {
        // Create output buffer with maximum possible size
        byte[] outputBuffer = new byte[data.Length];

        // Decrypt and get the actual length
        int actualLength = Decrypt(data, key, outputBuffer, iv, rounds);

        // Return a correctly sized array
        if (actualLength == outputBuffer.Length)
            return outputBuffer;

        byte[] result = new byte[actualLength];
        Buffer.BlockCopy(outputBuffer, 0, result, 0, actualLength);

        // Clear the original buffer for security
        Array.Clear(outputBuffer, 0, outputBuffer.Length);

        return result;
    }

    /// <summary>
    /// Decrypts the specified data using the XTEA algorithm, handling PKCS#7 padding.
    /// </summary>
    /// <param name="data">The data to decrypt (must be a multiple of 8 bytes).</param>
    /// <param name="key">The decryption key (must be exactly 16 bytes).</param>
    /// <param name="iv">Optional initialization vector for CBC mode. If null, ECB mode is used.</param>
    /// <param name="rounds">The Number of rounds to use (default is 32).</param>
    /// <returns>The decrypted data with padding removed.</returns>
    public static byte[] Decrypt(
        ReadOnlySpan<byte> data,
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> iv = default,
        int rounds = DefaultNumRounds)
    {
        if (key.Length != KeySizeInBytes)
            throw new ArgumentException($"Key must be exactly {KeySizeInBytes} bytes", nameof(key));

        // Convert the key bytes to words
        Span<uint> keyWords = stackalloc uint[KeySizeInWords];
        for (int i = 0; i < KeySizeInWords; i++)
        {
            keyWords[i] = BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(i * 4, 4));
        }

        return Decrypt(data, keyWords, iv, rounds);
    }

    #endregion
}
