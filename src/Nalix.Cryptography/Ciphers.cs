using Nalix.Common.Cryptography;
using Nalix.Common.Exceptions;
using Nalix.Cryptography.Aead;
using Nalix.Cryptography.Internal;
using Nalix.Cryptography.Symmetric.Block;
using Nalix.Cryptography.Symmetric.Stream;
using Nalix.Shared.Randomization;

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
    public static System.ReadOnlyMemory<byte> Encrypt(
        System.ReadOnlyMemory<byte> data, byte[] key,
        SymmetricAlgorithmType algorithm)
    {
        if (key == null)
            throw new System.ArgumentNullException(
                nameof(key), "Encryption key cannot be null. Please provide a valid key.");

        if (data.IsEmpty)
            throw new System.ArgumentException(
                "Data cannot be empty. Please provide data to encrypt.", nameof(data));

        if (!System.Enum.IsDefined(algorithm))
            throw new CryptoException($"The specified encryption algorithm '{algorithm}' is not supported.");

        try
        {
            switch (algorithm)
            {
                case SymmetricAlgorithmType.ChaCha20Poly1305:
                    {
                        System.Span<byte> nonce = RandGenerator.CreateNonce();

                        ChaCha20Poly1305.Encrypt(key, nonce, data.Span, null,
                            out byte[] ciphertext, out byte[] tag);

                        byte[] result = new byte[12 + ciphertext.Length + 16]; // 12 for nonce, 16 for tag
                        nonce.CopyTo(result);

                        System.Array.Copy(ciphertext, 0, result, 12, ciphertext.Length);
                        System.Array.Copy(tag, 0, result, 12 + ciphertext.Length, 16);

                        return result;
                    }

                case SymmetricAlgorithmType.Salsa20:
                    {
                        ulong counter = 0;
                        System.Span<byte> nonce = new byte[8];
                        byte[] ciphertext = new byte[data.Length];

                        Salsa20.Encrypt(key, nonce, counter, data.Span, ciphertext);

                        return ciphertext;
                    }

                case SymmetricAlgorithmType.Speck:
                    {
                        const int blockSize = 8;
                        int originalLength = data.Length;
                        if (originalLength == 0)
                            throw new System.ArgumentException("Input data cannot be empty.");

                        int bufferSize = (originalLength + 7) & ~7; // Align to 8-byte block
                        byte[] output = System.Buffers.ArrayPool<byte>.Shared.Rent(4 + bufferSize); // 4 bytes for length prefix

                        try
                        {
                            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(
                                System.MemoryExtensions.AsSpan(output, 0, 4), originalLength);
                            System.Span<byte> workSpan = System.MemoryExtensions.AsSpan(output, 4, bufferSize);

                            data.Span.CopyTo(workSpan);

                            if (bufferSize > originalLength)
                            {
                                RandGenerator.Fill(workSpan[originalLength..bufferSize]);
                            }

                            System.ReadOnlySpan<byte> fixedKey = BitwiseUtils.FixedSize(key);

                            for (int i = 0; i < bufferSize / blockSize; i++)
                            {
                                System.Span<byte> block = workSpan.Slice(i * blockSize, blockSize);
                                Speck.Encrypt(block, fixedKey, block);
                            }

                            return System.MemoryExtensions.AsSpan(output, 0, 4 + bufferSize).ToArray();
                        }
                        finally
                        {
                            System.Buffers.ArrayPool<byte>.Shared.Return(output);
                        }
                    }

                case SymmetricAlgorithmType.TwofishECB:
                    {
                        const int blockSize = 16;
                        int originalLength = data.Length;

                        if (originalLength == 0)
                            throw new System.ArgumentException("Input data cannot be empty.", nameof(data));

                        int paddedLength = (originalLength + blockSize - 1) & ~(blockSize - 1); // align to 16 bytes
                        byte[] output = System.Buffers.ArrayPool<byte>.Shared.Rent(4 + paddedLength); // 4 bytes for original length

                        try
                        {
                            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(
                                System.MemoryExtensions.AsSpan(output, 0, 4), originalLength);

                            System.Span<byte> workSpan = System.MemoryExtensions.AsSpan(output, 4, paddedLength);
                            data.Span.CopyTo(workSpan);

                            if (paddedLength > originalLength)
                            {
                                RandGenerator.Fill(workSpan[originalLength..paddedLength]);
                            }

                            byte[] encrypted = Twofish.ECB.Encrypt(key, workSpan);
                            encrypted.CopyTo(output, 4);

                            return System.MemoryExtensions.AsMemory(output, 0, 4 + paddedLength);
                        }
                        finally
                        {
                            System.Buffers.ArrayPool<byte>.Shared.Return(output);
                        }
                    }

                case SymmetricAlgorithmType.TwofishCBC:
                    {
                        const int blockSize = 16;
                        int originalLength = data.Length;

                        if (originalLength == 0)
                            throw new System.ArgumentException("Input data cannot be empty.", nameof(data));

                        int paddedLength = (originalLength + blockSize - 1) & ~(blockSize - 1); // Align to 16 bytes
                        System.Span<byte> iv = RandGenerator.CreateNonce(16); // 16 bytes IV

                        // 4 bytes for original length + 16 bytes IV + padded content
                        byte[] output = System.Buffers.ArrayPool<byte>.Shared.Rent(4 + 16 + paddedLength);

                        try
                        {
                            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(
                                System.MemoryExtensions.AsSpan(output, 0, 4), originalLength); // original data length
                            iv.CopyTo(System.MemoryExtensions.AsSpan(output, 4, 16)); // write IV

                            System.Span<byte> workSpan = System.MemoryExtensions.AsSpan(output, 4 + 16, paddedLength);
                            data.Span.CopyTo(workSpan);

                            if (paddedLength > originalLength)
                            {
                                RandGenerator.Fill(workSpan[originalLength..paddedLength]); // random padding
                            }

                            byte[] encrypted = Twofish.CBC.Encrypt(key, iv, workSpan);
                            encrypted.CopyTo(output, 4 + 16); // overwrite with encrypted data

                            return System.MemoryExtensions.AsMemory(output, 0, 4 + 16 + paddedLength);
                        }
                        finally
                        {
                            System.Buffers.ArrayPool<byte>.Shared.Return(output);
                        }
                    }

                case SymmetricAlgorithmType.XTEA:
                    {
                        int originalLength = data.Length;
                        if (originalLength == 0)
                            throw new System.ArgumentException("Input data cannot be empty.");

                        int bufferSize = (originalLength + 7) & ~7; // Align to 8-byte block

                        // Use ArrayPool to avoid frequent allocations for large data
                        byte[] encrypted = System.Buffers.ArrayPool<byte>.Shared.Rent(4 + bufferSize);
                        try
                        {
                            // WriteInt16 original length prefix
                            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(
                                System.MemoryExtensions.AsSpan(encrypted, 0, 4), originalLength);

                            // Avoid unnecessary allocation for paddedInput
                            System.ReadOnlySpan<byte> inputSpan = data.Span;
                            // Use heap memory for padding to avoid stack overflow
                            byte[] paddedInput = System.Buffers.ArrayPool<byte>.Shared.Rent(bufferSize);

                            try
                            {
                                inputSpan.CopyTo(paddedInput);
                                // Random padding instead of zero-padding for better security
                                RandGenerator.Fill(
                                    System.MemoryExtensions.AsSpan(paddedInput, originalLength, bufferSize - originalLength));

                                Xtea.Encrypt(
                                    System.MemoryExtensions.AsSpan(paddedInput, 0, bufferSize),
                                    BitwiseUtils.FixedSize(key),
                                    System.MemoryExtensions.AsSpan(encrypted, 4));
                            }
                            finally
                            {
                                System.Buffers.ArrayPool<byte>.Shared.Return(paddedInput);
                            }

                            // Return only the required portion of encrypted data
                            return System.MemoryExtensions.AsSpan(encrypted, 0, 4 + bufferSize).ToArray();
                        }
                        finally
                        {
                            System.Buffers.ArrayPool<byte>.Shared.Return(encrypted);
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
    public static System.ReadOnlyMemory<byte> Decrypt(
        System.ReadOnlyMemory<byte> data, byte[] key,
        SymmetricAlgorithmType algorithm = SymmetricAlgorithmType.XTEA)
    {
        if (key == null)
            throw new System.ArgumentNullException(nameof(key), "Decryption key cannot be null. Please provide a valid key.");

        if (data.IsEmpty)
            throw new System.ArgumentException("Data cannot be empty. Please provide the encrypted data to decrypt.", nameof(data));

        if (!System.Enum.IsDefined(algorithm))
            throw new CryptoException($"The specified decryption algorithm '{algorithm}' is not supported.");

        try
        {
            switch (algorithm)
            {
                case SymmetricAlgorithmType.ChaCha20Poly1305:
                    {
                        System.ReadOnlySpan<byte> input = data.Span;
                        if (input.Length < 28) // Min size = 12 (nonce) + 16 (tag)
                            throw new System.ArgumentException(
                                "Invalid data length. Encrypted data must contain a nonce (12 bytes) and a tag (16 bytes).",
                                nameof(data));

                        System.ReadOnlySpan<byte> nonce = input[..12];
                        System.ReadOnlySpan<byte> tag = input.Slice(input.Length - 16, 16);
                        System.ReadOnlySpan<byte> ciphertext = input[12..^16];

                        if (!ChaCha20Poly1305.Decrypt(key, nonce, ciphertext, null, tag, out byte[] plaintext))
                            throw new CryptoException(
                                "Decryption failed. Security of the encrypted data has failed.");

                        return plaintext;
                    }

                case SymmetricAlgorithmType.Salsa20:
                    {
                        ulong counter = 0;
                        System.Span<byte> nonce = new byte[8];
                        byte[] plaintext = new byte[data.Length];

                        Salsa20.Decrypt(key, nonce, counter, data.Span, plaintext);

                        return plaintext;
                    }

                case SymmetricAlgorithmType.Speck:
                    {
                        const int blockSize = 8;

                        if (data.Length < 4)
                            throw new System.ArgumentException("Input data too short to contain length prefix.");

                        System.ReadOnlySpan<byte> input = data.Span;

                        int originalLength = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(input[..4]);

                        int bufferSize = data.Length - 4;
                        if (originalLength < 0 || originalLength > bufferSize)
                            throw new System.ArgumentException("Invalid length prefix.");

                        if (bufferSize % blockSize != 0)
                            throw new System.ArgumentException("Input data length is not aligned to block size.");

                        byte[] rented = System.Buffers.ArrayPool<byte>.Shared.Rent(originalLength);
                        var output = new System.Span<byte>(rented, 0, originalLength);

                        try
                        {
                            // Slice phần mã hóa
                            System.Span<byte> workSpan = new(rented, 0, bufferSize);
                            System.ReadOnlySpan<byte> encrypted = input[4..];
                            encrypted.CopyTo(workSpan); // Copy vào buffer để giải mã in-place

                            // Key đã fixed size
                            System.ReadOnlySpan<byte> fixedKey = BitwiseUtils.FixedSize(key);

                            for (int i = 0; i < bufferSize / blockSize; i++)
                            {
                                System.Span<byte> block = workSpan.Slice(i * blockSize, blockSize);
                                Speck.Decrypt(block, fixedKey, block); // In-place decryption
                            }

                            return new System.ReadOnlyMemory<byte>(rented, 0, originalLength);
                        }
                        catch
                        {
                            System.Buffers.ArrayPool<byte>.Shared.Return(rented);
                            throw;
                        }
                    }

                case SymmetricAlgorithmType.TwofishECB:
                    {
                        const int blockSize = 16;

                        if (data.Length < 4 || (data.Length - 4) % blockSize != 0)
                            throw new System.ArgumentException("Invalid encrypted data format.", nameof(data));

                        int originalLength = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(data.Span[..4]);

                        if (originalLength < 0 || originalLength > data.Length - 4)
                            throw new System.ArgumentOutOfRangeException(nameof(data), "Invalid original length.");

                        System.ReadOnlySpan<byte> encryptedSpan = data.Span[4..];
                        byte[] decrypted = Twofish.ECB.Decrypt(key, encryptedSpan);

                        return System.MemoryExtensions.AsMemory(decrypted, 0, originalLength); // remove padding
                    }

                case SymmetricAlgorithmType.TwofishCBC:
                    {
                        const int blockSize = 16;

                        if (data.Length < 4 + 16)
                            throw new System.ArgumentException("Encrypted data is too short.", nameof(data));

                        System.ReadOnlySpan<byte> input = data.Span;

                        int originalLength = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(input[..4]);
                        if (originalLength < 0)
                            throw new System.ArgumentException("Invalid original length found in encrypted data.", nameof(data));

                        System.ReadOnlySpan<byte> iv = input.Slice(4, 16);
                        System.ReadOnlySpan<byte> encrypted = input[(4 + 16)..];

                        if (encrypted.Length % blockSize != 0)
                            throw new System.ArgumentException("Encrypted data length is not aligned to block size.", nameof(data));

                        byte[] decrypted = Twofish.CBC.Decrypt(key, iv, encrypted);

                        if (originalLength > decrypted.Length)
                            throw new CryptoException("Decrypted data is smaller than original length.");

                        return System.MemoryExtensions.AsMemory(decrypted, 0, originalLength);
                    }

                case SymmetricAlgorithmType.XTEA:
                    {
                        if (data.Length < 4)
                            throw new CryptoException("Invalid encrypted data format.");

                        int originalLength = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(data.Span[..4]);
                        int encryptedLength = data.Length - 4;

                        if (originalLength < 0 || originalLength > encryptedLength)
                            throw new CryptoException("Corrupted length header.");

                        byte[] decrypted = System.Buffers.ArrayPool<byte>.Shared.Rent(encryptedLength);

                        try
                        {
                            Xtea.Decrypt(data.Span[4..], BitwiseUtils.FixedSize(key),
                                System.MemoryExtensions.AsSpan(decrypted, 0, encryptedLength));

                            return System.MemoryExtensions.AsMemory(decrypted, 0, originalLength); // Trim padding
                        }
                        finally
                        {
                            System.Buffers.ArrayPool<byte>.Shared.Return(decrypted);
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
    public static bool TryEncrypt(
        System.ReadOnlyMemory<byte> data, byte[] key,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)]
        out System.ReadOnlyMemory<byte> memory, SymmetricAlgorithmType mode)
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
    public static bool TryDecrypt(
        System.ReadOnlyMemory<byte> data, byte[] key,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)]
        out System.ReadOnlyMemory<byte> memory, SymmetricAlgorithmType mode)
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