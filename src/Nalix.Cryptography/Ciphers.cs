// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Exceptions;
using Nalix.Common.Security.Cryptography;
using Nalix.Cryptography.Aead;
using Nalix.Cryptography.Internal;
using Nalix.Cryptography.Symmetric.Block;
using Nalix.Cryptography.Symmetric.Stream;
using Nalix.Framework.Randomization;

namespace Nalix.Cryptography;

/// <summary>
/// Provides methods to encrypt and decrypt raw data.
/// </summary>
public static class Ciphers
{
    #region Constants

    private const System.Int32 ChaCha20NonceSize = 12;
    private const System.Int32 ChaCha20TagSize = 16;
    private const System.Int32 Salsa20NonceSize = 8;
    private const System.Int32 SpeckBlockSize = 8;
    private const System.Int32 TwofishBlockSize = 16;
    private const System.Int32 XteaBlockSize = 8;
    private const System.Int32 LengthPrefixSize = 4;

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
        SymmetricAlgorithmType algorithm)
    {
        ValidateEncryptionInputs(data, key, algorithm);

        try
        {
            return algorithm switch
            {
                SymmetricAlgorithmType.ChaCha20Poly1305 => EncryptChaCha20Poly1305(data, key),
                SymmetricAlgorithmType.Salsa20 => EncryptSalsa20(data, key),
                SymmetricAlgorithmType.Speck => EncryptSpeck(data, key),
                SymmetricAlgorithmType.TwofishECB => EncryptTwofishECB(data, key),
                SymmetricAlgorithmType.TwofishCBC => EncryptTwofishCBC(data, key),
                SymmetricAlgorithmType.XTEA => EncryptXTEA(data, key),
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
        SymmetricAlgorithmType algorithm = SymmetricAlgorithmType.XTEA)
    {
        ValidateDecryptionInputs(data, key, algorithm);

        try
        {
            return algorithm switch
            {
                SymmetricAlgorithmType.ChaCha20Poly1305 => DecryptChaCha20Poly1305(data, key),
                SymmetricAlgorithmType.Salsa20 => DecryptSalsa20(data, key),
                SymmetricAlgorithmType.Speck => DecryptSpeck(data, key),
                SymmetricAlgorithmType.TwofishECB => DecryptTwofishECB(data, key),
                SymmetricAlgorithmType.TwofishCBC => DecryptTwofishCBC(data, key),
                SymmetricAlgorithmType.XTEA => DecryptXTEA(data, key),
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
        SymmetricAlgorithmType mode)
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
        SymmetricAlgorithmType mode)
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

    #region Validation Methods

    private static void ValidateEncryptionInputs(System.ReadOnlyMemory<System.Byte> data, System.Byte[] key, SymmetricAlgorithmType algorithm)
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
    }

    private static void ValidateDecryptionInputs(System.ReadOnlyMemory<System.Byte> data, System.Byte[] key, SymmetricAlgorithmType algorithm)
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
    }

    #endregion

    #region Encryption Methods

    private static System.ReadOnlyMemory<System.Byte> EncryptChaCha20Poly1305(
        System.ReadOnlyMemory<System.Byte> data, System.Byte[] key)
    {
        System.Span<System.Byte> nonce = SecureRandom.CreateNonce(ChaCha20NonceSize);

        ChaCha20Poly1305.Encrypt(key, nonce, data.Span, null,
            out System.Byte[] ciphertext, out System.Byte[] tag);

        var result = new System.Byte[ChaCha20NonceSize + ciphertext.Length + ChaCha20TagSize];
        nonce.CopyTo(result);
        System.Array.Copy(ciphertext, 0, result, ChaCha20NonceSize, ciphertext.Length);
        System.Array.Copy(tag, 0, result, ChaCha20NonceSize + ciphertext.Length, ChaCha20TagSize);

        return result;
    }

    private static System.ReadOnlyMemory<System.Byte> EncryptSalsa20(
        System.ReadOnlyMemory<System.Byte> data, System.Byte[] key)
    {
        const System.UInt64 counter = 0;
        System.Span<System.Byte> nonce = new System.Byte[Salsa20NonceSize];
        var ciphertext = new System.Byte[data.Length];

        _ = Salsa20.Encrypt(key, nonce, counter, data.Span, ciphertext);
        return ciphertext;
    }

    private static System.ReadOnlyMemory<System.Byte> EncryptSpeck(
        System.ReadOnlyMemory<System.Byte> data, System.Byte[] key)
    {
        System.Int32 originalLength = data.Length;
        System.Int32 bufferSize = AlignToBlockSize(originalLength, SpeckBlockSize);

        System.Byte[] output = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(LengthPrefixSize + bufferSize);

        try
        {
            WriteLengthPrefix(output, originalLength);
            System.Span<System.Byte> workSpan = System.MemoryExtensions.AsSpan(output, LengthPrefixSize, bufferSize);

            data.Span.CopyTo(workSpan);
            if (bufferSize > originalLength)
            {
                SecureRandom.Fill(workSpan[originalLength..bufferSize]);
            }

            System.ReadOnlySpan<System.Byte> fixedKey = BitwiseUtils.FixedSize(key);
            EncryptBlocks(workSpan, fixedKey, SpeckBlockSize, (block, k, output) =>
                Speck.Encrypt(block, k, output));

            return System.MemoryExtensions.AsSpan(output, 0, LengthPrefixSize + bufferSize).ToArray();
        }
        finally
        {
            System.Buffers.ArrayPool<System.Byte>.Shared.Return(output);
        }
    }

    private static System.ReadOnlyMemory<System.Byte> EncryptTwofishECB(
        System.ReadOnlyMemory<System.Byte> data, System.Byte[] key)
    {
        System.Int32 originalLength = data.Length;
        System.Int32 paddedLength = AlignToBlockSize(originalLength, TwofishBlockSize);

        System.Byte[] output = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(LengthPrefixSize + paddedLength);

        try
        {
            WriteLengthPrefix(output, originalLength);
            System.Span<System.Byte> workSpan = System.MemoryExtensions.AsSpan(output, LengthPrefixSize, paddedLength);

            data.Span.CopyTo(workSpan);
            if (paddedLength > originalLength)
            {
                SecureRandom.Fill(workSpan[originalLength..paddedLength]);
            }

            System.Byte[] encrypted = Twofish.ECB.Encrypt(key, workSpan);
            encrypted.CopyTo(output, LengthPrefixSize);

            return System.MemoryExtensions.AsSpan(output, 0, LengthPrefixSize + paddedLength).ToArray();
        }
        finally
        {
            System.Buffers.ArrayPool<System.Byte>.Shared.Return(output);
        }
    }

    private static System.ReadOnlyMemory<System.Byte> EncryptTwofishCBC(
        System.ReadOnlyMemory<System.Byte> data, System.Byte[] key)
    {
        System.Int32 originalLength = data.Length;
        System.Int32 paddedLength = AlignToBlockSize(originalLength, TwofishBlockSize);
        System.Span<System.Byte> iv = SecureRandom.CreateNonce(TwofishBlockSize);

        const System.Int32 headerSize = LengthPrefixSize + TwofishBlockSize;
        System.Byte[] output = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(headerSize + paddedLength);

        try
        {
            WriteLengthPrefix(output, originalLength);
            iv.CopyTo(System.MemoryExtensions.AsSpan(output, LengthPrefixSize, TwofishBlockSize));

            System.Span<System.Byte> workSpan = System.MemoryExtensions.AsSpan(output, headerSize, paddedLength);
            data.Span.CopyTo(workSpan);

            if (paddedLength > originalLength)
            {
                SecureRandom.Fill(workSpan[originalLength..paddedLength]);
            }

            System.Byte[] encrypted = Twofish.CBC.Encrypt(key, iv, workSpan);
            encrypted.CopyTo(output, headerSize);

            return System.MemoryExtensions.AsSpan(output, 0, headerSize + paddedLength).ToArray();
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
            WriteLengthPrefix(encrypted, originalLength);

            data.Span.CopyTo(paddedInput);
            SecureRandom.Fill(System.MemoryExtensions.AsSpan(paddedInput, originalLength, bufferSize - originalLength));

            _ = Xtea.Encrypt(
                System.MemoryExtensions.AsSpan(paddedInput, 0, bufferSize),
                BitwiseUtils.FixedSize(key),
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
        const System.Int32 minSize = ChaCha20NonceSize + ChaCha20TagSize;

        if (input.Length < minSize)
        {
            throw new System.ArgumentException(
                $"Invalid data length. Encrypted data must contain a nonce ({ChaCha20NonceSize} bytes) and a tag ({ChaCha20TagSize} bytes).",
                nameof(data));
        }

        System.ReadOnlySpan<System.Byte> nonce = input[..ChaCha20NonceSize];
        System.ReadOnlySpan<System.Byte> tag = input.Slice(input.Length - ChaCha20TagSize, ChaCha20TagSize);
        System.ReadOnlySpan<System.Byte> ciphertext = input.Slice(ChaCha20NonceSize, input.Length - ChaCha20NonceSize - ChaCha20TagSize);

        return !ChaCha20Poly1305.Decrypt(key, nonce, ciphertext, null, tag, out System.Byte[] plaintext)
            ? throw new CryptoException("Decryption failed. Security of the encrypted data has failed.")
            : (System.ReadOnlyMemory<System.Byte>)plaintext;
    }

    private static System.ReadOnlyMemory<System.Byte> DecryptSalsa20(
        System.ReadOnlyMemory<System.Byte> data, System.Byte[] key)
    {
        const System.UInt64 counter = 0;
        System.Span<System.Byte> nonce = new System.Byte[Salsa20NonceSize];
        var plaintext = new System.Byte[data.Length];

        _ = Salsa20.Decrypt(key, nonce, counter, data.Span, plaintext);
        return plaintext;
    }

    private static System.ReadOnlyMemory<System.Byte> DecryptSpeck(
        System.ReadOnlyMemory<System.Byte> data, System.Byte[] key)
    {
        ValidateDataWithLengthPrefix(data, SpeckBlockSize);

        System.ReadOnlySpan<System.Byte> input = data.Span;
        System.Int32 originalLength = ReadLengthPrefix(input);
        System.Int32 bufferSize = data.Length - LengthPrefixSize;

        ValidateOriginalLength(originalLength, bufferSize);
        ValidateBlockAlignment(bufferSize, SpeckBlockSize);

        System.Byte[] rented = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(bufferSize);

        try
        {
            System.Span<System.Byte> workSpan = System.MemoryExtensions.AsSpan(rented, 0, bufferSize);
            System.ReadOnlySpan<System.Byte> encrypted = input[LengthPrefixSize..];
            encrypted.CopyTo(workSpan);

            System.ReadOnlySpan<System.Byte> fixedKey = BitwiseUtils.FixedSize(key);
            DecryptBlocks(workSpan, fixedKey, SpeckBlockSize, (block, k, output) =>
                Speck.Decrypt(block, k, output));

            return workSpan[..originalLength].ToArray();
        }
        finally
        {
            System.Buffers.ArrayPool<System.Byte>.Shared.Return(rented);
        }
    }

    private static System.ReadOnlyMemory<System.Byte> DecryptTwofishECB(
        System.ReadOnlyMemory<System.Byte> data, System.Byte[] key)
    {
        ValidateDataWithLengthPrefix(data, TwofishBlockSize);

        System.Int32 originalLength = ReadLengthPrefix(data.Span);
        ValidateOriginalLength(originalLength, data.Length - LengthPrefixSize);

        System.ReadOnlySpan<System.Byte> encryptedSpan = data.Span[LengthPrefixSize..];
        System.Byte[] decrypted = Twofish.ECB.Decrypt(key, encryptedSpan);

        return System.MemoryExtensions.AsSpan(decrypted, 0, originalLength).ToArray();
    }

    private static System.ReadOnlyMemory<System.Byte> DecryptTwofishCBC(
        System.ReadOnlyMemory<System.Byte> data, System.Byte[] key)
    {
        const System.Int32 headerSize = LengthPrefixSize + TwofishBlockSize;

        if (data.Length < headerSize)
        {
            throw new System.ArgumentException("Encrypted data is too short.", nameof(data));
        }

        System.ReadOnlySpan<System.Byte> input = data.Span;
        System.Int32 originalLength = ReadLengthPrefix(input);

        if (originalLength < 0)
        {
            throw new System.ArgumentException("Invalid original length found in encrypted data.", nameof(data));
        }

        System.ReadOnlySpan<System.Byte> iv = input.Slice(LengthPrefixSize, TwofishBlockSize);
        System.ReadOnlySpan<System.Byte> encrypted = input[headerSize..];

        ValidateBlockAlignment(encrypted.Length, TwofishBlockSize);

        System.Byte[] decrypted = Twofish.CBC.Decrypt(key, iv, encrypted);

        return originalLength > decrypted.Length
            ? throw new CryptoException("Decrypted data is smaller than original length.")
            : (System.ReadOnlyMemory<System.Byte>)System.MemoryExtensions.AsSpan(decrypted, 0, originalLength).ToArray();
    }

    private static System.ReadOnlyMemory<System.Byte> DecryptXTEA(
        System.ReadOnlyMemory<System.Byte> data, System.Byte[] key)
    {
        ValidateDataWithLengthPrefix(data);

        System.Int32 originalLength = ReadLengthPrefix(data.Span);
        System.Int32 encryptedLength = data.Length - LengthPrefixSize;

        ValidateOriginalLength(originalLength, encryptedLength);

        System.Byte[] decrypted = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(encryptedLength);

        try
        {
            _ = Xtea.Decrypt(data.Span[LengthPrefixSize..], BitwiseUtils.FixedSize(key),
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

    private static System.Int32 AlignToBlockSize(System.Int32 length, System.Int32 blockSize) =>
        (length + blockSize - 1) & ~(blockSize - 1);

    private static void WriteLengthPrefix(System.Span<System.Byte> buffer, System.Int32 length) =>
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(buffer[..LengthPrefixSize], length);

    private static System.Int32 ReadLengthPrefix(System.ReadOnlySpan<System.Byte> buffer) =>
        System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(buffer[..LengthPrefixSize]);

    private static void ValidateDataWithLengthPrefix(System.ReadOnlyMemory<System.Byte> data, System.Int32 blockSize = 1)
    {
        if (data.Length < LengthPrefixSize)
        {
            throw new System.ArgumentException("Input data too short to contain length prefix.", nameof(data));
        }

        if (blockSize > 1 && (data.Length - LengthPrefixSize) % blockSize != 0)
        {
            throw new System.ArgumentException("Input data length is not aligned to block size.", nameof(data));
        }
    }

    private static void ValidateOriginalLength(System.Int32 originalLength, System.Int32 maxLength)
    {
        if (originalLength < 0 || originalLength > maxLength)
        {
            throw new System.ArgumentException("Invalid length prefix.");
        }
    }

    private static void ValidateBlockAlignment(System.Int32 length, System.Int32 blockSize)
    {
        if (length % blockSize != 0)
        {
            throw new System.ArgumentException("Data length is not aligned to block size.");
        }
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