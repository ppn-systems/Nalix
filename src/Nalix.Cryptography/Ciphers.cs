using Nalix.Common.Cryptography;
using Nalix.Common.Exceptions;
using Nalix.Cryptography.Aead;
using Nalix.Cryptography.Symmetric;
using Nalix.Randomization;
using System;
using System.Buffers;
using System.Buffers.Binary;

namespace Nalix.Cryptography;

/// <summary>
/// Provides methods to encrypt and decrypt raw data.
/// </summary>
public static class Ciphers
{
    /// <summary>
    /// Encrypts the provided data using the specified algorithm.
    /// </summary>
    /// <param name="data">The input data as <see cref="ReadOnlyMemory{Byte}"/>.</param>
    /// <param name="key">The encryption key.</param>
    /// <param name="algorithm">
    /// The encryption algorithm to use.
    /// </param>
    /// <returns>The encrypted data as <see cref="ReadOnlyMemory{Byte}"/>.</returns>
    public static Memory<byte> Encrypt(
        Memory<byte> data, byte[] key,
        EncryptionType algorithm)
    {
        if (key == null)
            throw new ArgumentNullException(
                nameof(key), "Encryption key cannot be null. Please provide a valid key.");

        if (data.IsEmpty)
            throw new ArgumentException(
                "Data cannot be empty. Please provide data to encrypt.", nameof(data));

        if (!Enum.IsDefined(algorithm))
            throw new CryptoException($"The specified encryption algorithm '{algorithm}' is not supported.");

        try
        {
            switch (algorithm)
            {
                case EncryptionType.ChaCha20Poly1305:
                    {
                        Span<byte> nonce = RandGenerator.CreateNonce();

                        ChaCha20Poly1305.Encrypt(key, nonce, data.Span, null,
                            out byte[] ciphertext, out byte[] tag);

                        byte[] result = new byte[12 + ciphertext.Length + 16]; // 12 for nonce, 16 for tag
                        nonce.CopyTo(result);
                        ciphertext.CopyTo(result.AsSpan(12));
                        tag.CopyTo(result.AsSpan(12 + ciphertext.Length));

                        return result;
                    }

                case EncryptionType.Salsa20:
                    {
                        Span<byte> nonce = RandGenerator.CreateNonce();
                        ulong counter = 0; // Typically starts at 0

                        byte[] ciphertext = new byte[data.Length];
                        Salsa20.Encrypt(key, nonce, counter, data.Span, ciphertext);

                        byte[] result = new byte[8 + ciphertext.Length]; // 8 for nonce
                        nonce.CopyTo(result);
                        ciphertext.CopyTo(result.AsSpan(8));

                        return result;
                    }

                case EncryptionType.Speck:
                    {
                        int blockSize = 8;
                        int bufferSize = (data.Length + blockSize - 1) & ~(blockSize - 1); // Align to blockSize

                        byte[] paddedData = new byte[bufferSize];
                        data.Span.CopyTo(paddedData);

                        byte[] output = new byte[paddedData.Length];
                        Span<byte> outputSpan = output.AsSpan();

                        for (int i = 0; i < paddedData.Length / blockSize; i++)
                        {
                            ReadOnlySpan<byte> block = paddedData.AsSpan().Slice(i * blockSize, blockSize);
                            Span<byte> destination = outputSpan.Slice(i * blockSize, blockSize);

                            Speck.Encrypt(block, Xtea.DeriveXteaKey(key), destination);
                        }

                        return output;
                    }

                case EncryptionType.TwofishECB:
                    {
                        if (data.Length % 16 != 0)
                            throw new ArgumentException(
                                "Data length must be a multiple of 16 bytes for Twofish ECB.", nameof(data));

                        byte[] encrypted = Twofish.ECB.Encrypt(key, data.Span);
                        return encrypted;
                    }

                case EncryptionType.TwofishCBC:
                    {
                        Span<byte> iv = RandGenerator.CreateNonce(16);
                        if (data.Length % 16 != 0)
                            throw new ArgumentException(
                                "Data length must be a multiple of 16 bytes for Twofish CBC.", nameof(data));

                        byte[] encrypted = Twofish.CBC.Encrypt(key, iv, data.Span);

                        byte[] result = new byte[16 + encrypted.Length]; // IV (16) + ciphertext
                        iv.CopyTo(result);
                        encrypted.CopyTo(result.AsSpan(16));

                        return result;
                    }

                case EncryptionType.XTEA:
                    {
                        int originalLength = data.Length;
                        int bufferSize = (originalLength + 7) & ~7; // Align to 8-byte block

                        Span<byte> paddedInput = stackalloc byte[bufferSize];
                        data.Span.CopyTo(paddedInput); // Zero-padding

                        byte[] encrypted = new byte[4 + bufferSize]; // 4 byte prefix + ciphertext
                        BinaryPrimitives.WriteInt32LittleEndian(encrypted.AsSpan(0, 4), originalLength);

                        // Encrypt into encrypted[4..]
                        Xtea.Encrypt(paddedInput, Xtea.DeriveXteaKey(key), encrypted.AsSpan(4));

                        return encrypted;
                    }

                default:
                    throw new CryptoException(
                        $"The specified encryption algorithm '{algorithm}' is not supported. " +
                        $"Please choose a valid algorithm.");
            }
        }
        catch (Exception ex)
        {
            throw new CryptoException(
                "Encryption failed. An unexpected error occurred during the encryption process.", ex);
        }
    }

    /// <summary>
    /// Decrypts the provided data using the specified algorithm.
    /// </summary>
    /// <param name="data">The encrypted data as <see cref="ReadOnlyMemory{Byte}"/>.</param>
    /// <param name="key">The decryption key.</param>
    /// <param name="algorithm">
    /// The encryption algorithm that was used.
    /// </param>
    /// <returns>The decrypted data as <see cref="ReadOnlyMemory{Byte}"/>.</returns>
    public static Memory<byte> Decrypt(
        Memory<byte> data, byte[] key,
        EncryptionType algorithm = EncryptionType.XTEA)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key), "Decryption key cannot be null. Please provide a valid key.");

        if (data.IsEmpty)
            throw new ArgumentException("Data cannot be empty. Please provide the encrypted data to decrypt.", nameof(data));

        if (!Enum.IsDefined(algorithm))
            throw new CryptoException($"The specified decryption algorithm '{algorithm}' is not supported.");

        try
        {
            switch (algorithm)
            {
                case EncryptionType.ChaCha20Poly1305:
                    {
                        ReadOnlySpan<byte> input = data.Span;
                        if (input.Length < 28) // Min size = 12 (nonce) + 16 (tag)
                            throw new ArgumentException(
                                "Invalid data length. " +
                                "Encrypted data must contain a nonce (12 bytes) and a tag (16 bytes).",
                                nameof(data));

                        ReadOnlySpan<byte> nonce = input[..12];
                        ReadOnlySpan<byte> tag = input.Slice(input.Length - 16, 16);
                        ReadOnlySpan<byte> ciphertext = input[12..^16];

                        if (!ChaCha20Poly1305.Decrypt(key, nonce, ciphertext, null, tag, out byte[] plaintext))
                            throw new CryptoException(
                                "Decryption failed. Security of the encrypted data has failed.");

                        return plaintext;
                    }

                case EncryptionType.Salsa20:
                    {
                        ReadOnlySpan<byte> input = data.Span;
                        if (input.Length < 8) // Min size = 8 (nonce)
                            throw new ArgumentException(
                                "Invalid data length. Encrypted data must contain a nonce (8 bytes).",
                                nameof(data));

                        ReadOnlySpan<byte> nonce = input[..8];
                        ReadOnlySpan<byte> ciphertext = input[8..];
                        ulong counter = 0; // Must match the encryption counter

                        byte[] plaintext = new byte[ciphertext.Length];
                        Salsa20.Decrypt(key, nonce, counter, ciphertext, plaintext);

                        return plaintext;
                    }

                case EncryptionType.Speck:
                    {
                        if (data.Length % 8 != 0)
                            throw new ArgumentException(
                                "Data length must be a multiple of 8 bytes for Speck encryption.", nameof(data));

                        byte[] output = new byte[data.Length];
                        Span<byte> outputSpan = output.AsSpan();

                        for (int i = 0; i < data.Length / 8; i++)
                        {
                            ReadOnlySpan<byte> block = data.Span.Slice(i * 8, 8);
                            Span<byte> destination = outputSpan.Slice(i * 8, 8);

                            Speck.Decrypt(block, Xtea.DeriveXteaKey(key), destination);
                        }

                        // Return only the original length (remove any padding)
                        return output.AsMemory(0, data.Length); // Remove padding
                    }

                case EncryptionType.TwofishECB:
                    {
                        if (data.Length % 16 != 0)
                            throw new ArgumentException(
                                "Data length must be a multiple of 16 bytes for Twofish ECB.", nameof(data));

                        byte[] decrypted = Twofish.ECB.Decrypt(key, data.Span);
                        return decrypted;
                    }

                case EncryptionType.TwofishCBC:
                    {
                        if (data.Length < 16 || (data.Length - 16) % 16 != 0)
                            throw new ArgumentException("Invalid data length for Twofish CBC.", nameof(data));

                        ReadOnlySpan<byte> iv = data.Span[..16];
                        ReadOnlySpan<byte> ciphertext = data.Span[16..];

                        byte[] decrypted = Twofish.CBC.Decrypt(key, iv, ciphertext);
                        return decrypted;
                    }

                case EncryptionType.XTEA:
                    {
                        if (data.Length < 4)
                            throw new CryptoException("Invalid encrypted data format.");

                        int originalLength = BinaryPrimitives.ReadInt32LittleEndian(data.Span[..4]);
                        int encryptedLength = data.Length - 4;

                        if (originalLength < 0 || originalLength > encryptedLength)
                            throw new CryptoException("Corrupted length header.");

                        byte[] decrypted = ArrayPool<byte>.Shared.Rent(encryptedLength);

                        try
                        {
                            Xtea.Decrypt(data.Span[4..], Xtea.DeriveXteaKey(key), decrypted.AsSpan(0, encryptedLength));
                            return decrypted.AsMemory(0, originalLength); // Trim padding
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(decrypted);
                        }
                    }

                default:
                    throw new CryptoException(
                        $"The specified encryption algorithm '{algorithm}' is not supported. " +
                        $"Please choose a valid algorithm.");
            }
        }
        catch (Exception ex)
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
    /// <param name="mode">The encryption mode to use. Default is <see cref="EncryptionType.XTEA"/>.</param>
    /// <returns><c>true</c> if encryption succeeded; otherwise, <c>false</c>.</returns>
    public static bool TryEncrypt(
        Memory<byte> data, byte[] key,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Memory<byte> memory,
        EncryptionType mode)
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
    /// <param name="mode">The encryption mode to use. Default is <see cref="EncryptionType.XTEA"/>.</param>
    /// <returns><c>true</c> if encryption succeeded; otherwise, <c>false</c>.</returns>
    public static bool TryDecrypt(
        Memory<byte> data, byte[] key,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Memory<byte> memory,
        EncryptionType mode)
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
