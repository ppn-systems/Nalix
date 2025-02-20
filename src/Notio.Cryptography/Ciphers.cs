using Notio.Common.Cryptography;
using Notio.Common.Exceptions;
using Notio.Cryptography.Symmetric;
using Notio.Randomization;
using System;
using System.Buffers;

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
    public static Memory<byte> Encrypt(Memory<byte> data, byte[] key, EncryptionMode algorithm = EncryptionMode.Xtea)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key), "Encryption key cannot be null. Please provide a valid key.");

        if (data.IsEmpty)
            throw new ArgumentException("Data cannot be empty. Please provide data to encrypt.", nameof(data));

        try
        {
            switch (algorithm)
            {
                case EncryptionMode.Xtea:
                    {
                        int bufferSize = (data.Length + 7) & ~7; // Align to 8-byte boundary
                        byte[] encryptedXtea = ArrayPool<byte>.Shared.Rent(bufferSize);

                        try
                        {
                            Xtea.Encrypt(data, RandomizedGenerator.ConvertKey(key), encryptedXtea.AsMemory(0, bufferSize));
                            return encryptedXtea.AsMemory(0, bufferSize);
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(encryptedXtea);
                        }
                    }

                case EncryptionMode.ChaCha20Poly1305:
                    {
                        Span<byte> nonce = RandomizedGenerator.CreateNonce();

                        ChaCha20Poly1305.Encrypt(key, nonce, data.Span, null, out byte[] ciphertext, out byte[] tag);

                        byte[] result = new byte[12 + ciphertext.Length + 16]; // 12 for nonce, 16 for tag
                        nonce.CopyTo(result);
                        ciphertext.CopyTo(result.AsSpan(12));
                        tag.CopyTo(result.AsSpan(12 + ciphertext.Length));

                        return result;
                    }

                default:
                    throw new CryptographicException(
                        $"The specified encryption algorithm '{algorithm}' is not supported. Please choose a valid algorithm.");
            }
        }
        catch (Exception ex)
        {
            throw new CryptographicException("Encryption failed. An unexpected error occurred during the encryption process.", ex);
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
    public static Memory<byte> Decrypt(Memory<byte> data, byte[] key, EncryptionMode algorithm = EncryptionMode.Xtea)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key), "Decryption key cannot be null. Please provide a valid key.");

        if (data.IsEmpty)
            throw new ArgumentException("Data cannot be empty. Please provide the encrypted data to decrypt.", nameof(data));

        try
        {
            switch (algorithm)
            {
                case EncryptionMode.Xtea:
                    {
                        int bufferSize = (data.Length + 7) & ~7; // Align to 8-byte boundary
                        byte[] decryptedXtea = ArrayPool<byte>.Shared.Rent(bufferSize);

                        try
                        {
                            Xtea.Decrypt(data, RandomizedGenerator.ConvertKey(key), decryptedXtea.AsMemory(0, bufferSize));

                            return decryptedXtea.AsMemory(0, bufferSize);
                        }
                        catch (Exception ex)
                        {
                            throw new CryptographicException("Decryption failed. Authentication of the data has failed.", ex);
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(decryptedXtea);
                        }
                    }

                case EncryptionMode.ChaCha20Poly1305:
                    {
                        ReadOnlySpan<byte> input = data.Span;
                        if (input.Length < 28) // Min size = 12 (nonce) + 16 (tag)
                            throw new ArgumentException(
                                "Invalid data length. Encrypted data must contain a nonce (12 bytes) and a tag (16 bytes).", nameof(data));

                        ReadOnlySpan<byte> nonce = input[..12];
                        ReadOnlySpan<byte> tag = input.Slice(input.Length - 16, 16);
                        ReadOnlySpan<byte> ciphertext = input[12..^16];

                        if (!ChaCha20Poly1305.Decrypt(key, nonce, ciphertext, null, tag, out byte[] plaintext))
                            throw new CryptographicException("Decryption failed. Authentication of the encrypted data has failed.");

                        return plaintext;
                    }

                default:
                    throw new CryptographicException(
                        $"The specified encryption algorithm '{algorithm}' is not supported. Please choose a valid algorithm.");
            }
        }
        catch (Exception ex)
        {
            throw new CryptographicException("Decryption failed. An unexpected error occurred during the decryption process.", ex);
        }
    }
}
