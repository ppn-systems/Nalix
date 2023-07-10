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
    /// <summary>
    /// Encrypts the provided data using the specified algorithm.
    /// </summary>
    /// <param name="data">The input data as <see cref="System.ReadOnlyMemory{Byte}"/>.</param>
    /// <param name="key">The encryption key.</param>
    /// <param name="algorithm">
    /// The encryption algorithm to use.
    /// </param>
    /// <returns>The encrypted data as <see cref="System.ReadOnlyMemory{Byte}"/>.</returns>
    public static System.ReadOnlyMemory<System.Byte> Encrypt(
        System.ReadOnlyMemory<System.Byte> data, System.Byte[] key,
        SymmetricAlgorithmType algorithm)
    {
        if (key == null)
        {
            throw new System.ArgumentNullException(
                nameof(key), "Encryption key cannot be null. Please provide a valid key.");
        }

        if (data.IsEmpty)
        {
            throw new System.ArgumentException(
                "Data cannot be empty. Please provide data to encrypt.", nameof(data));
        }

        if (!System.Enum.IsDefined(algorithm))
        {
            throw new CryptoException($"The specified encryption algorithm '{algorithm}' is not supported.");
        }

        try
        {
            switch (algorithm)
            {
                case SymmetricAlgorithmType.ChaCha20Poly1305:
                    {
                        System.Span<System.Byte> nonce = SecureRandom.CreateNonce();

                        ChaCha20Poly1305.Encrypt(key, nonce, data.Span, null,
                            out System.Byte[] ciphertext, out System.Byte[] tag);

                        System.Byte[] result = new System.Byte[12 + ciphertext.Length + 16]; // 12 for nonce, 16 for tag
                        nonce.CopyTo(result);

                        System.Array.Copy(ciphertext, 0, result, 12, ciphertext.Length);
                        System.Array.Copy(tag, 0, result, 12 + ciphertext.Length, 16);

                        return result;
                    }

                case SymmetricAlgorithmType.Salsa20:
                    {
                        System.UInt64 counter = 0;
                        System.Span<System.Byte> nonce = new System.Byte[8];
                        System.Byte[] ciphertext = new System.Byte[data.Length];

                        _ = Salsa20.Encrypt(key, nonce, counter, data.Span, ciphertext);

                        return ciphertext;
                    }

                case SymmetricAlgorithmType.Speck:
                    {
                        const System.Int32 blockSize = 8;
                        System.Int32 originalLength = data.Length;
                        if (originalLength == 0)
                        {
                            throw new System.ArgumentException("Input data cannot be empty.");
                        }

                        System.Int32 bufferSize = (originalLength + 7) & ~7; // Align to 8-byte block
                        System.Byte[] output = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(4 + bufferSize); // 4 bytes for length prefix

                        try
                        {
                            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(
                                System.MemoryExtensions.AsSpan(output, 0, 4), originalLength);
                            System.Span<System.Byte> workSpan = System.MemoryExtensions.AsSpan(output, 4, bufferSize);

                            data.Span.CopyTo(workSpan);

                            if (bufferSize > originalLength)
                            {
                                SecureRandom.Fill(workSpan[originalLength..bufferSize]);
                            }

                            System.ReadOnlySpan<System.Byte> fixedKey = BitwiseUtils.FixedSize(key);

                            for (System.Int32 i = 0; i < bufferSize / blockSize; i++)
                            {
                                System.Span<System.Byte> block = workSpan.Slice(i * blockSize, blockSize);
                                Speck.Encrypt(block, fixedKey, block);
                            }

                            return System.MemoryExtensions.AsSpan(output, 0, 4 + bufferSize).ToArray();
                        }
                        finally
                        {
                            System.Buffers.ArrayPool<System.Byte>.Shared.Return(output);
                        }
                    }

                case SymmetricAlgorithmType.TwofishECB:
                    {
                        const System.Int32 blockSize = 16;
                        System.Int32 originalLength = data.Length;

                        if (originalLength == 0)
                        {
                            throw new System.ArgumentException("Input data cannot be empty.", nameof(data));
                        }

                        System.Int32 paddedLength = (originalLength + blockSize - 1) & ~(blockSize - 1); // align to 16 bytes
                        System.Byte[] output = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(4 + paddedLength); // 4 bytes for original length

                        try
                        {
                            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(
                                System.MemoryExtensions.AsSpan(output, 0, 4), originalLength);

                            System.Span<System.Byte> workSpan = System.MemoryExtensions.AsSpan(output, 4, paddedLength);
                            data.Span.CopyTo(workSpan);

                            if (paddedLength > originalLength)
                            {
                                SecureRandom.Fill(workSpan[originalLength..paddedLength]);
                            }

                            System.Byte[] encrypted = Twofish.ECB.Encrypt(key, workSpan);
                            encrypted.CopyTo(output, 4);

                            return System.MemoryExtensions.AsMemory(output, 0, 4 + paddedLength);
                        }
                        finally
                        {
                            System.Buffers.ArrayPool<System.Byte>.Shared.Return(output);
                        }
                    }

                case SymmetricAlgorithmType.TwofishCBC:
                    {
                        const System.Int32 blockSize = 16;
                        System.Int32 originalLength = data.Length;

                        if (originalLength == 0)
                        {
                            throw new System.ArgumentException("Input data cannot be empty.", nameof(data));
                        }

                        System.Int32 paddedLength = (originalLength + blockSize - 1) & ~(blockSize - 1); // Align to 16 bytes
                        System.Span<System.Byte> iv = SecureRandom.CreateNonce(16); // 16 bytes IV

                        // 4 bytes for original length + 16 bytes IV + padded content
                        System.Byte[] output = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(4 + 16 + paddedLength);

                        try
                        {
                            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(
                                System.MemoryExtensions.AsSpan(output, 0, 4), originalLength); // original data length
                            iv.CopyTo(System.MemoryExtensions.AsSpan(output, 4, 16)); // write IV

                            System.Span<System.Byte> workSpan = System.MemoryExtensions.AsSpan(output, 4 + 16, paddedLength);
                            data.Span.CopyTo(workSpan);

                            if (paddedLength > originalLength)
                            {
                                SecureRandom.Fill(workSpan[originalLength..paddedLength]); // random padding
                            }

                            System.Byte[] encrypted = Twofish.CBC.Encrypt(key, iv, workSpan);
                            encrypted.CopyTo(output, 4 + 16); // overwrite with encrypted data

                            return System.MemoryExtensions.AsMemory(output, 0, 4 + 16 + paddedLength);
                        }
                        finally
                        {
                            System.Buffers.ArrayPool<System.Byte>.Shared.Return(output);
                        }
                    }

                case SymmetricAlgorithmType.XTEA:
                    {
                        System.Int32 originalLength = data.Length;
                        if (originalLength == 0)
                        {
                            throw new System.ArgumentException("Input data cannot be empty.");
                        }

                        System.Int32 bufferSize = (originalLength + 7) & ~7; // Align to 8-byte block

                        // Use ArrayPool to avoid frequent allocations for large data
                        System.Byte[] encrypted = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(4 + bufferSize);
                        try
                        {
                            // WriteInt16 original length prefix
                            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(
                                System.MemoryExtensions.AsSpan(encrypted, 0, 4), originalLength);

                            // Avoid unnecessary allocation for paddedInput
                            System.ReadOnlySpan<System.Byte> inputSpan = data.Span;
                            // Use heap memory for padding to avoid stack overflow
                            System.Byte[] paddedInput = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(bufferSize);

                            try
                            {
                                inputSpan.CopyTo(paddedInput);
                                // Random padding instead of zero-padding for better security
                                SecureRandom.Fill(
                                    System.MemoryExtensions.AsSpan(paddedInput, originalLength, bufferSize - originalLength));

                                _ = Xtea.Encrypt(
                                    System.MemoryExtensions.AsSpan(paddedInput, 0, bufferSize),
                                    BitwiseUtils.FixedSize(key),
                                    System.MemoryExtensions.AsSpan(encrypted, 4));
                            }
                            finally
                            {
                                System.Buffers.ArrayPool<System.Byte>.Shared.Return(paddedInput);
                            }

                            // Return only the required portion of encrypted data
                            return System.MemoryExtensions.AsSpan(encrypted, 0, 4 + bufferSize).ToArray();
                        }
                        finally
                        {
                            System.Buffers.ArrayPool<System.Byte>.Shared.Return(encrypted);
                        }
                    }

                default:
                    throw new CryptoException(
                        $"The specified encryption algorithm '{algorithm}' is not supported. " +
                        $"Please choose a valid algorithm.");
            }
        }
        catch (System.Exception ex)
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
    /// <param name="algorithm">
    /// The encryption algorithm that was used.
    /// </param>
    /// <returns>The decrypted data as <see cref="System.ReadOnlyMemory{Byte}"/>.</returns>
    public static System.ReadOnlyMemory<System.Byte> Decrypt(
        System.ReadOnlyMemory<System.Byte> data, System.Byte[] key,
        SymmetricAlgorithmType algorithm = SymmetricAlgorithmType.XTEA)
    {
        if (key == null)
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
            switch (algorithm)
            {
                case SymmetricAlgorithmType.ChaCha20Poly1305:
                    {
                        System.ReadOnlySpan<System.Byte> input = data.Span;
                        if (input.Length < 28) // Min size = 12 (nonce) + 16 (tag)
                        {
                            throw new System.ArgumentException(
                                "Invalid data length. Encrypted data must contain a nonce (12 bytes) and a tag (16 bytes).",
                                nameof(data));
                        }

                        System.ReadOnlySpan<System.Byte> nonce = input[..12];
                        System.ReadOnlySpan<System.Byte> tag = input.Slice(input.Length - 16, 16);
                        System.ReadOnlySpan<System.Byte> ciphertext = input[12..^16];

                        return !ChaCha20Poly1305.Decrypt(key, nonce, ciphertext, null, tag, out System.Byte[] plaintext)
                            ? throw new CryptoException(
                                "Decryption failed. Security of the encrypted data has failed.")
                            : (System.ReadOnlyMemory<System.Byte>)plaintext;
                    }

                case SymmetricAlgorithmType.Salsa20:
                    {
                        System.UInt64 counter = 0;
                        System.Span<System.Byte> nonce = new System.Byte[8];
                        System.Byte[] plaintext = new System.Byte[data.Length];

                        _ = Salsa20.Decrypt(key, nonce, counter, data.Span, plaintext);

                        return plaintext;
                    }

                case SymmetricAlgorithmType.Speck:
                    {
                        const System.Int32 blockSize = 8;

                        if (data.Length < 4)
                        {
                            throw new System.ArgumentException("Input data too short to contain length prefix.");
                        }

                        System.ReadOnlySpan<System.Byte> input = data.Span;

                        System.Int32 originalLength = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(input[..4]);

                        System.Int32 bufferSize = data.Length - 4;
                        if (originalLength < 0 || originalLength > bufferSize)
                        {
                            throw new System.ArgumentException("Invalid length prefix.");
                        }

                        if (bufferSize % blockSize != 0)
                        {
                            throw new System.ArgumentException("Input data length is not aligned to block size.");
                        }

                        System.Byte[] rented = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(originalLength);
                        var output = new System.Span<System.Byte>(rented, 0, originalLength);

                        try
                        {
                            // Slice phần mã hóa
                            System.Span<System.Byte> workSpan = new(rented, 0, bufferSize);
                            System.ReadOnlySpan<System.Byte> encrypted = input[4..];
                            encrypted.CopyTo(workSpan); // Copy vào buffer để giải mã in-place

                            // Key đã fixed size
                            System.ReadOnlySpan<System.Byte> fixedKey = BitwiseUtils.FixedSize(key);

                            for (System.Int32 i = 0; i < bufferSize / blockSize; i++)
                            {
                                System.Span<System.Byte> block = workSpan.Slice(i * blockSize, blockSize);
                                Speck.Decrypt(block, fixedKey, block); // In-place decryption
                            }

                            return new System.ReadOnlyMemory<System.Byte>(rented, 0, originalLength);
                        }
                        catch
                        {
                            System.Buffers.ArrayPool<System.Byte>.Shared.Return(rented);
                            throw;
                        }
                    }

                case SymmetricAlgorithmType.TwofishECB:
                    {
                        const System.Int32 blockSize = 16;

                        if (data.Length < 4 || (data.Length - 4) % blockSize != 0)
                        {
                            throw new System.ArgumentException("Invalid encrypted data format.", nameof(data));
                        }

                        System.Int32 originalLength = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(data.Span[..4]);

                        if (originalLength < 0 || originalLength > data.Length - 4)
                        {
                            throw new System.ArgumentOutOfRangeException(nameof(data), "Invalid original length.");
                        }

                        System.ReadOnlySpan<System.Byte> encryptedSpan = data.Span[4..];
                        System.Byte[] decrypted = Twofish.ECB.Decrypt(key, encryptedSpan);

                        return System.MemoryExtensions.AsMemory(decrypted, 0, originalLength); // remove padding
                    }

                case SymmetricAlgorithmType.TwofishCBC:
                    {
                        const System.Int32 blockSize = 16;

                        if (data.Length < 4 + 16)
                        {
                            throw new System.ArgumentException("Encrypted data is too short.", nameof(data));
                        }

                        System.ReadOnlySpan<System.Byte> input = data.Span;

                        System.Int32 originalLength = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(input[..4]);
                        if (originalLength < 0)
                        {
                            throw new System.ArgumentException("Invalid original length found in encrypted data.", nameof(data));
                        }

                        System.ReadOnlySpan<System.Byte> iv = input.Slice(4, 16);
                        System.ReadOnlySpan<System.Byte> encrypted = input[(4 + 16)..];

                        if (encrypted.Length % blockSize != 0)
                        {
                            throw new System.ArgumentException("Encrypted data length is not aligned to block size.", nameof(data));
                        }

                        System.Byte[] decrypted = Twofish.CBC.Decrypt(key, iv, encrypted);

                        return originalLength > decrypted.Length
                            ? throw new CryptoException("Decrypted data is smaller than original length.")
                            : (System.ReadOnlyMemory<System.Byte>)System.MemoryExtensions.AsMemory(decrypted, 0, originalLength);
                    }

                case SymmetricAlgorithmType.XTEA:
                    {
                        if (data.Length < 4)
                        {
                            throw new CryptoException("Invalid encrypted data format.");
                        }

                        System.Int32 originalLength = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(data.Span[..4]);
                        System.Int32 encryptedLength = data.Length - 4;

                        if (originalLength < 0 || originalLength > encryptedLength)
                        {
                            throw new CryptoException("Corrupted length header.");
                        }

                        System.Byte[] decrypted = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(encryptedLength);

                        try
                        {
                            _ = Xtea.Decrypt(data.Span[4..], BitwiseUtils.FixedSize(key),
                                System.MemoryExtensions.AsSpan(decrypted, 0, encryptedLength));

                            return System.MemoryExtensions.AsMemory(decrypted, 0, originalLength); // Trim padding
                        }
                        finally
                        {
                            System.Buffers.ArrayPool<System.Byte>.Shared.Return(decrypted);
                        }
                    }

                default:
                    throw new CryptoException(
                        $"The specified encryption algorithm '{algorithm}' is not supported. " +
                        $"Please choose a valid algorithm.");
            }
        }
        catch (System.Exception ex)
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
    /// <param name="memory">
    /// When this method returns, contains the encrypted data if encryption succeeded; otherwise, the default value.
    /// </param>
    /// <param name="mode">The encryption mode to use. Default is <see cref="SymmetricAlgorithmType.XTEA"/>.</param>
    /// <returns><c>true</c> if encryption succeeded; otherwise, <c>false</c>.</returns>
    public static System.Boolean TryEncrypt(
        System.ReadOnlyMemory<System.Byte> data, System.Byte[] key,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)]
        out System.ReadOnlyMemory<System.Byte> memory, SymmetricAlgorithmType mode)
    {
        try
        {
            memory = Encrypt(data, key, mode);
            return true;
        }
        catch
        {
            memory = default;
            return false;
        }
    }

    /// <summary>
    /// Attempts to decrypt the specified data using the provided key and encryption mode.
    /// </summary>
    /// <param name="data">The input data to encrypt.</param>
    /// <param name="key">The encryption key.</param>
    /// <param name="memory">
    /// When this method returns, contains the encrypted data if encryption succeeded; otherwise, the default value.
    /// </param>
    /// <param name="mode">The encryption mode to use. Default is <see cref="SymmetricAlgorithmType.XTEA"/>.</param>
    /// <returns><c>true</c> if encryption succeeded; otherwise, <c>false</c>.</returns>
    public static System.Boolean TryDecrypt(
        System.ReadOnlyMemory<System.Byte> data, System.Byte[] key,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)]
        out System.ReadOnlyMemory<System.Byte> memory, SymmetricAlgorithmType mode)
    {
        try
        {
            memory = Decrypt(data, key, mode);
            return true;
        }
        catch
        {
            memory = default;
            return false;
        }
    }
}