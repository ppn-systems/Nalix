using Notio.Common.Cryptography;
using Notio.Common.Exceptions;
using Notio.Cryptography.Aead;
using Notio.Cryptography.Symmetric;
using Notio.Randomization;
using System;
using System.Diagnostics.CodeAnalysis;

namespace Notio.Cryptography;

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
        EncryptionType algorithm = EncryptionType.XTEA)
    {
        if (key == null)
            throw new ArgumentNullException(
                nameof(key), "Encryption key cannot be null. Please provide a valid key.");

        if (data.IsEmpty)
            throw new ArgumentException(
                "Data cannot be empty. Please provide data to encrypt.", nameof(data));

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
                        // Prepare output buffer
                        byte[] output = new byte[data.Length];
                        int blockSize = 8; // Speck operates on 8-byte blocks
                        int fullBlocks = data.Length / blockSize;

                        // Process data in 8-byte blocks
                        for (int i = 0; i < fullBlocks; i++)
                        {
                            // Get the current 8-byte block
                            ReadOnlySpan<byte> block = data.Span.Slice(i * blockSize, blockSize);
                            Span<byte> ciphertext = new byte[blockSize];

                            // Encrypt the block
                            Speck.Encrypt(block, key, ciphertext);

                            // Copy encrypted block to the output
                            ciphertext.CopyTo(output.AsSpan(i * blockSize));
                        }

                        return output;
                    }

                case EncryptionType.SpeckCBC:
                    {
                        // Prepare output buffer
                        byte[] output = new byte[data.Length];
                        int blockSize = 8; // Speck operates on 8-byte blocks
                        int fullBlocks = data.Length / blockSize;

                        // Process data in 8-byte blocks
                        for (int i = 0; i < fullBlocks; i++)
                        {
                            // Get the current 8-byte block
                            ReadOnlySpan<byte> block = data.Span.Slice(i * blockSize, blockSize);
                            Span<byte> ciphertext = new byte[blockSize];

                            // Encrypt the block
                            Speck.CBC.Encrypt(block, key, ciphertext);

                            // Copy encrypted block to the output
                            ciphertext.CopyTo(output.AsSpan(i * blockSize));
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
                        int bufferSize = (data.Length + 7) & ~7; // Align to 8-byte boundary
                        byte[] encryptedXtea = System.Buffers.ArrayPool<byte>.Shared.Rent(bufferSize);

                        try
                        {
                            Xtea.Encrypt(data.Span, RandGenerator.ConvertKey(key), encryptedXtea.AsSpan()[..bufferSize]);
                            return encryptedXtea.AsMemory(0, bufferSize);
                        }
                        finally
                        {
                            System.Buffers.ArrayPool<byte>.Shared.Return(encryptedXtea);
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
            throw new ArgumentNullException(nameof(key),
                "Decryption key cannot be null. Please provide a valid key.");

        if (data.IsEmpty)
            throw new ArgumentException(
                "Data cannot be empty. Please provide the encrypted data to decrypt.", nameof(data));

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
                        // Prepare output buffer
                        byte[] output = new byte[data.Length];
                        int blockSize = 8; // Speck operates on 8-byte blocks
                        int fullBlocks = data.Length / blockSize;

                        // Process data in 8-byte blocks
                        for (int i = 0; i < fullBlocks; i++)
                        {
                            // Get the current 8-byte block
                            ReadOnlySpan<byte> block = data.Span.Slice(i * blockSize, blockSize);
                            Span<byte> plaintext = new byte[blockSize];

                            // Decrypt the block
                            Speck.Decrypt(block, key, plaintext);

                            // Copy decrypted block to the output
                            plaintext.CopyTo(output.AsSpan(i * blockSize));
                        }

                        return output;
                    }

                case EncryptionType.SpeckCBC:
                    {
                        // Prepare output buffer
                        byte[] output = new byte[data.Length];
                        int blockSize = 8; // Speck operates on 8-byte blocks
                        int fullBlocks = data.Length / blockSize;

                        // Process data in 8-byte blocks
                        for (int i = 0; i < fullBlocks; i++)
                        {
                            // Get the current 8-byte block
                            ReadOnlySpan<byte> block = data.Span.Slice(i * blockSize, blockSize);
                            Span<byte> plaintext = new byte[blockSize];

                            // Decrypt the block
                            Speck.CBC.Decrypt(block, key, plaintext);

                            // Copy decrypted block to the output
                            plaintext.CopyTo(output.AsSpan(i * blockSize));
                        }

                        return output;
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
                        int bufferSize = (data.Length + 7) & ~7; // Align to 8-byte boundary
                        byte[] decryptedXtea = System.Buffers.ArrayPool<byte>.Shared.Rent(bufferSize);

                        try
                        {
                            Xtea.Decrypt(data.Span, RandGenerator.ConvertKey(key),
                                decryptedXtea.AsSpan()[..bufferSize]);

                            return decryptedXtea.AsMemory(0, bufferSize);
                        }
                        catch (Exception ex)
                        {
                            throw new CryptoException(
                                "Decryption failed. Security of the data has failed.", ex);
                        }
                        finally
                        {
                            System.Buffers.ArrayPool<byte>.Shared.Return(decryptedXtea);
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
    public static bool TryEncrypt(Memory<byte> data, byte[] key,
        [NotNullWhen(true)] out Memory<byte> memory, EncryptionType mode = EncryptionType.XTEA)
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
    public static bool TryDecrypt(Memory<byte> data, byte[] key,
        [NotNullWhen(true)] out Memory<byte> memory, EncryptionType mode = EncryptionType.XTEA)
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
