// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Enums;
using Nalix.Common.Exceptions;
using Nalix.Framework.Cryptography.Aead;
using Nalix.Framework.Cryptography.Primitives;
using Nalix.Framework.Cryptography.Symmetric;
using Nalix.Framework.Randomization;

namespace Nalix.Framework.Cryptography;

/// <summary>
/// Provides methods to encrypt and decrypt raw data.
/// </summary>
public static class Crypto
{
    #region Constants

    private const System.Int32 XteaBlockSize = 8;
    private const System.Int32 SpeckBlockSize = 8;
    private const System.Int32 Salsa20NonceSize = 8;
    private const System.Int32 LengthPrefixSize = 4;
    private const System.Int32 ChaCha20NonceSize = 12;

    #endregion Constants

    #region APIs

    /// <summary>
    /// Encrypts the provided data using the specified algorithm.
    /// </summary>
    /// <param name="data">The input data as <see cref="System.ReadOnlyMemory{Byte}"/>.</param>
    /// <param name="key">The encryption key.</param>
    /// <param name="algorithm">The encryption algorithm to use.</param>
    /// <returns>The encrypted data as <see cref="System.ReadOnlyMemory{Byte}"/>.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when key is null.</exception>
    /// <exception cref="System.ArgumentException">Thrown when data is empty.</exception>
    /// <exception cref="CryptoException">Thrown when algorithm is not supported or encryption fails.</exception>
    public static System.ReadOnlyMemory<System.Byte> Encrypt(
        System.ReadOnlyMemory<System.Byte> data,
        System.Byte[] key,
        CipherType algorithm)
    {
        if (key is null)
        {
            throw new System.ArgumentNullException(nameof(key), "Encryption key cannot be null. Please provide a valid key.");
        }

        if (data.IsEmpty)
        {
            throw new System.ArgumentException("Data cannot be empty. Please provide data to encrypt.", nameof(data));
        }

        if (!System.Enum.IsDefined(algorithm))
        {
            throw new CryptoException($"The specified encryption algorithm '{algorithm}' is not supported.");
        }

        try
        {
            return algorithm switch
            {
                CipherType.ChaCha20Poly1305 => EncryptChaCha20Poly1305(data, key),
                CipherType.Salsa20 => EncryptSalsa20(data, key),
                CipherType.Speck => EncryptSpeck(data, key),
                CipherType.XTEA => EncryptXTEA(data, key),
                _ => throw new CryptoException(
                    $"The specified encryption algorithm '{algorithm}' is not supported.")
            };
        }
        catch (System.Exception ex) when (ex is not CryptoException)
        {
            throw new CryptoException(
                "Encryption failed. An unexpected error occurred during the encryption process.", ex);
        }
    }

    /// <summary>
    /// Decrypts the provided data using the specified algorithm.
    /// </summary>
    /// <param name="data">The encrypted data as <see cref="System.ReadOnlyMemory{Byte}"/>.</param>
    /// <param name="key">The decryption key.</param>
    /// <param name="algorithm">The encryption algorithm that was used.</param>
    /// <returns>The decrypted data as <see cref="System.ReadOnlyMemory{Byte}"/>.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when key is null.</exception>
    /// <exception cref="System.ArgumentException">Thrown when data is empty.</exception>
    /// <exception cref="CryptoException">Thrown when algorithm is not supported or decryption fails.</exception>
    public static System.ReadOnlyMemory<System.Byte> Decrypt(
        System.ReadOnlyMemory<System.Byte> data,
        System.Byte[] key,
        CipherType algorithm = CipherType.XTEA)
    {
        if (key is null)
        {
            throw new System.ArgumentNullException(nameof(key), "Decryption key cannot be null. Please provide a valid key.");
        }

        if (data.IsEmpty)
        {
            throw new System.ArgumentException("Data cannot be empty. Please provide the encrypted data to decrypt.", nameof(data));
        }

        if (!System.Enum.IsDefined(algorithm))
        {
            throw new CryptoException($"The specified decryption algorithm '{algorithm}' is not supported.");
        }

        try
        {
            return algorithm switch
            {
                CipherType.ChaCha20Poly1305 => DecryptChaCha20Poly1305(data, key),
                CipherType.Salsa20 => DecryptSalsa20(data, key),
                CipherType.Speck => DecryptSpeck(data, key),
                CipherType.XTEA => DecryptXTEA(data, key),
                _ => throw new CryptoException(
                    $"The specified decryption algorithm '{algorithm}' is not supported.")
            };
        }
        catch (System.Exception ex) when (ex is not CryptoException)
        {
            throw new CryptoException(
                "Decryption failed. An unexpected error occurred during the decryption process.", ex);
        }
    }

    /// <summary>
    /// Attempts to encrypt the specified data using the provided key and encryption mode.
    /// </summary>
    /// <param name="data">The input data to encrypt.</param>
    /// <param name="key">The encryption key.</param>
    /// <param name="result">When this method returns, contains the encrypted data if encryption succeeded; otherwise, default.</param>
    /// <param name="mode">The encryption mode to use.</param>
    /// <returns><c>true</c> if encryption succeeded; otherwise, <c>false</c>.</returns>
    public static System.Boolean TryEncrypt(
        System.ReadOnlyMemory<System.Byte> data,
        System.Byte[] key,
        out System.ReadOnlyMemory<System.Byte> result,
        CipherType mode)
    {
        try
        {
            result = Encrypt(data, key, mode);
            return true;
        }
        catch
        {
            result = default;
            return false;
        }
    }

    /// <summary>
    /// Attempts to decrypt the specified data using the provided key and encryption mode.
    /// </summary>
    /// <param name="data">The encrypted data to decrypt.</param>
    /// <param name="key">The decryption key.</param>
    /// <param name="result">When this method returns, contains the decrypted data if decryption succeeded; otherwise, default.</param>
    /// <param name="mode">The encryption mode to use.</param>
    /// <returns><c>true</c> if decryption succeeded; otherwise, <c>false</c>.</returns>
    public static System.Boolean TryDecrypt(
        System.ReadOnlyMemory<System.Byte> data,
        System.Byte[] key,
        out System.ReadOnlyMemory<System.Byte> result,
        CipherType mode)
    {
        try
        {
            result = Decrypt(data, key, mode);
            return true;
        }
        catch
        {
            result = default;
            return false;
        }
    }

    #endregion APIs

    #region Encryption Methods

    private static System.ReadOnlyMemory<System.Byte> EncryptChaCha20Poly1305(
        System.ReadOnlyMemory<System.Byte> data, System.Byte[] key)
    {
        // Nonce 12 bytes (unique per key!)
        System.Span<System.Byte> nonce = stackalloc System.Byte[ChaCha20NonceSize];
        SecureRandom.Fill(nonce);

        System.Byte[] result = new System.Byte[ChaCha20NonceSize + data.Length + ChaCha20Poly1305.TagSize];

        // layout: [ nonce | ciphertext | tag ]
        nonce.CopyTo(System.MemoryExtensions.AsSpan(result, 0, ChaCha20NonceSize));
        System.Span<System.Byte> ctSpan = System.MemoryExtensions.AsSpan(result, ChaCha20NonceSize, data.Length);
        System.Span<System.Byte> tagSpan = System.MemoryExtensions.AsSpan(result, ChaCha20NonceSize + data.Length, ChaCha20Poly1305.TagSize);

        ChaCha20Poly1305.Encrypt(key, nonce, data.Span, [], ctSpan, tagSpan);

        return result;
    }

    private static System.ReadOnlyMemory<System.Byte> EncryptSalsa20(
        System.ReadOnlyMemory<System.Byte> data, System.Byte[] key)
    {
        const System.UInt64 counter = 0;
        System.Span<System.Byte> nonce = new System.Byte[Salsa20NonceSize];
        SecureRandom.Fill(nonce);

        System.Byte[] result = new System.Byte[Salsa20NonceSize + data.Length];
        nonce.CopyTo(result);

        _ = Salsa20.Encrypt(key, nonce, counter, data.Span, System.MemoryExtensions.AsSpan(result));
        return result;
    }

    private static System.ReadOnlyMemory<System.Byte> EncryptSpeck(
        System.ReadOnlyMemory<System.Byte> data, System.Byte[] key)
    {
        System.Int32 originalLength = data.Length;
        System.Int32 bufferSize = AlignToBlockSize(originalLength, SpeckBlockSize);

        System.Byte[] output = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(LengthPrefixSize + bufferSize);

        try
        {
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(System.MemoryExtensions
                                                  .AsSpan(output)[..LengthPrefixSize], originalLength);

            System.Span<System.Byte> workSpan = System.MemoryExtensions.AsSpan(output, LengthPrefixSize, bufferSize);

            data.Span.CopyTo(workSpan);
            if (bufferSize > originalLength)
            {
                SecureRandom.Fill(workSpan[originalLength..bufferSize]);
            }

            EncryptBlocks(
                workSpan,
                BitwiseOperations.FixedSize(key), SpeckBlockSize,
                (block, k, output) => Speck.Encrypt(block, k, output));

            return System.MemoryExtensions.AsSpan(output, 0, LengthPrefixSize + bufferSize).ToArray();
        }
        finally
        {
            System.Buffers.ArrayPool<System.Byte>.Shared.Return(output);
        }
    }
    private static System.ReadOnlyMemory<System.Byte> EncryptXTEA(
        System.ReadOnlyMemory<System.Byte> data, System.Byte[] key)
    {
        System.Int32 originalLength = data.Length;
        System.Int32 bufferSize = AlignToBlockSize(originalLength, XteaBlockSize);

        System.Byte[] encrypted = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(LengthPrefixSize + bufferSize);
        System.Byte[] paddedInput = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(bufferSize);

        try
        {
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(System.MemoryExtensions
                                                  .AsSpan(encrypted)[..LengthPrefixSize], originalLength);

            data.Span.CopyTo(paddedInput);
            SecureRandom.Fill(System.MemoryExtensions.AsSpan(paddedInput, originalLength, bufferSize - originalLength));

            _ = Xtea.Encrypt(
                System.MemoryExtensions.AsSpan(paddedInput, 0, bufferSize),
                BitwiseOperations.FixedSize(key),
                System.MemoryExtensions.AsSpan(encrypted, LengthPrefixSize));

            return System.MemoryExtensions.AsSpan(encrypted, 0, LengthPrefixSize + bufferSize).ToArray();
        }
        finally
        {
            System.Buffers.ArrayPool<System.Byte>.Shared.Return(encrypted);
            System.Buffers.ArrayPool<System.Byte>.Shared.Return(paddedInput);
        }
    }

    #endregion

    #region Decryption Methods

    private static System.ReadOnlyMemory<System.Byte> DecryptChaCha20Poly1305(
        System.ReadOnlyMemory<System.Byte> data, System.Byte[] key)
    {
        System.ReadOnlySpan<System.Byte> input = data.Span;
        const System.Int32 minSize = ChaCha20NonceSize + ChaCha20Poly1305.TagSize;

        if (input.Length < minSize)
        {
            throw new System.ArgumentException(
                 "Invalid data length. " +
                $"Encrypted data must contain a nonce ({ChaCha20NonceSize} bytes) and a tag ({ChaCha20Poly1305.TagSize} bytes).",
                nameof(data));
        }

        // Extract layout: nonce || ciphertext || tag
        System.ReadOnlySpan<System.Byte> nonce = input[..ChaCha20NonceSize];
        System.ReadOnlySpan<System.Byte> tag = input.Slice(input.Length - ChaCha20Poly1305.TagSize, ChaCha20Poly1305.TagSize);
        System.ReadOnlySpan<System.Byte> ciphertext = input.Slice(
            ChaCha20NonceSize,
            input.Length - ChaCha20NonceSize - ChaCha20Poly1305.TagSize);

        // Prepare plaintext buffer
        System.Byte[] plaintext = new System.Byte[ciphertext.Length];

        System.Boolean ok = ChaCha20Poly1305.Decrypt(
            key,
            nonce,
            ciphertext,
            [], // no AAD
            tag,
            plaintext);

        return !ok ? throw new CryptoException("Decryption failed. Authentication tag mismatch.") : (System.ReadOnlyMemory<System.Byte>)plaintext;
    }

    private static System.ReadOnlyMemory<System.Byte> DecryptSalsa20(
        System.ReadOnlyMemory<System.Byte> data, System.Byte[] key)
    {
        const System.UInt64 counter = 0;

        System.ReadOnlySpan<System.Byte> input = data.Span;

        System.ReadOnlySpan<System.Byte> nonce = input[..Salsa20NonceSize];
        System.ReadOnlySpan<System.Byte> ciphertext = input[Salsa20NonceSize..];

        System.Byte[] plaintext = new System.Byte[ciphertext.Length];

        _ = Salsa20.Decrypt(key, nonce, counter, data.Span, plaintext);
        return plaintext;
    }

    private static System.ReadOnlyMemory<System.Byte> DecryptSpeck(
        System.ReadOnlyMemory<System.Byte> data, System.Byte[] key)
    {
        if (data.Length < LengthPrefixSize)
        {
            throw new System.ArgumentException("Input data too short to contain length prefix.", nameof(data));
        }

        if (SpeckBlockSize > 1 && (data.Length - LengthPrefixSize) % SpeckBlockSize != 0)
        {
            throw new System.ArgumentException("Input data length is not aligned to block size.", nameof(data));
        }

        System.ReadOnlySpan<System.Byte> input = data.Span;
        System.Int32 originalLength = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(input[..LengthPrefixSize]);
        System.Int32 bufferSize = data.Length - LengthPrefixSize;

        if (originalLength < 0 || originalLength > bufferSize)
        {
            throw new System.ArgumentException("Invalid length prefix.");
        }
        if (bufferSize % SpeckBlockSize != 0)
        {
            throw new System.ArgumentException("Data length is not aligned to block size.");
        }

        System.Byte[] rented = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(bufferSize);

        try
        {
            System.Span<System.Byte> workSpan = System.MemoryExtensions.AsSpan(rented, 0, bufferSize);
            System.ReadOnlySpan<System.Byte> encrypted = input[LengthPrefixSize..];
            encrypted.CopyTo(workSpan);

            DecryptBlocks(
                workSpan,
                BitwiseOperations.FixedSize(key), SpeckBlockSize,
                (block, k, output) => Speck.Decrypt(block, k, output));

            return workSpan[..originalLength].ToArray();
        }
        finally
        {
            System.Buffers.ArrayPool<System.Byte>.Shared.Return(rented);
        }
    }

    private static System.ReadOnlyMemory<System.Byte> DecryptXTEA(
        System.ReadOnlyMemory<System.Byte> data, System.Byte[] key)
    {
        if (data.Length < LengthPrefixSize)
        {
            throw new System.ArgumentException("Input data too short to contain length prefix.", nameof(data));
        }

        if (SpeckBlockSize > 1 && (data.Length - LengthPrefixSize) % SpeckBlockSize != 0)
        {
            throw new System.ArgumentException("Input data length is not aligned to block size.", nameof(data));
        }

        System.Int32 originalLength = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(data.Span[..LengthPrefixSize]);
        System.Int32 encryptedLength = data.Length - LengthPrefixSize;

        if (originalLength < 0 || originalLength > encryptedLength)
        {
            throw new System.ArgumentException("Invalid length prefix.");
        }

        System.Byte[] decrypted = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(encryptedLength);

        try
        {
            _ = Xtea.Decrypt(data.Span[LengthPrefixSize..], BitwiseOperations.FixedSize(key),
                System.MemoryExtensions.AsSpan(decrypted, 0, encryptedLength));

            return System.MemoryExtensions.AsSpan(decrypted, 0, originalLength).ToArray();
        }
        finally
        {
            System.Buffers.ArrayPool<System.Byte>.Shared.Return(decrypted);
        }
    }

    #endregion

    #region Helper Methods

    private static System.Int32 AlignToBlockSize(System.Int32 length, System.Int32 blockSize)
    {
        return (blockSize & blockSize - 1) != 0
            ? throw new System.ArgumentOutOfRangeException(nameof(blockSize), "Block size must be a power of two.")
            : length + blockSize - 1 & ~(blockSize - 1);
    }

    private static void EncryptBlocks(
        System.Span<System.Byte> data, System.ReadOnlySpan<System.Byte> key, System.Int32 blockSize,
        System.Action<System.Span<System.Byte>, System.ReadOnlySpan<System.Byte>, System.Span<System.Byte>> encryptBlock)
    {
        for (System.Int32 i = 0; i < data.Length / blockSize; i++)
        {
            System.Span<System.Byte> block = data.Slice(i * blockSize, blockSize);
            encryptBlock(block, key, block);
        }
    }

    private static void DecryptBlocks(
        System.Span<System.Byte> data, System.ReadOnlySpan<System.Byte> key, System.Int32 blockSize,
        System.Action<System.Span<System.Byte>, System.ReadOnlySpan<System.Byte>, System.Span<System.Byte>> decryptBlock)
    {
        for (System.Int32 i = 0; i < data.Length / blockSize; i++)
        {
            System.Span<System.Byte> block = data.Slice(i * blockSize, blockSize);
            decryptBlock(block, key, block);
        }
    }

    #endregion
}