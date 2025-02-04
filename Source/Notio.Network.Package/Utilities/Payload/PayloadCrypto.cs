using Notio.Common;
using Notio.Common.Exceptions;
using Notio.Cryptography.Ciphers;
using Notio.Cryptography.Ciphers.Symmetric;
using Notio.Network.Package.Enums;
using System;

namespace Notio.Network.Package.Utilities.Payload;

/// <summary>
/// Provides methods to encrypt and decrypt raw data payloads.
/// </summary>
public static class PayloadCrypto
{
    /// <summary>
    /// Encrypts the provided data using the specified algorithm.
    /// </summary>
    /// <param name="data">The input data as <see cref="ReadOnlyMemory{Byte}"/>.</param>
    /// <param name="key">The encryption key.</param>
    /// <param name="algorithm">
    /// The encryption algorithm to use.
    /// (Example: Xtea, AesGcm, ChaCha20Poly1305)
    /// </param>
    /// <returns>The encrypted data as <see cref="ReadOnlyMemory{Byte}"/>.</returns>
    public static ReadOnlyMemory<byte> Encrypt(ReadOnlyMemory<byte> data, byte[] key, PacketEncryptionMode algorithm = PacketEncryptionMode.Xtea)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key), "Key cannot be null.");
        if (data.IsEmpty)
            throw new ArgumentException("Data cannot be empty.", nameof(data));

        try
        {
            switch (algorithm)
            {
                case PacketEncryptionMode.Xtea:
                    {
                        // Calculate the required buffer size (round up to the next multiple of 8)
                        int bufferSize = data.Length + 7 & ~7;
                        Memory<byte> encryptedXtea = new byte[bufferSize];
                        Xtea.Encrypt(data, key.ConvertKey(), encryptedXtea);
                        return encryptedXtea;
                    }

                case PacketEncryptionMode.AesGcm:
                    {
                        // Encrypt using AES-256 GCM mode
                        ReadOnlyMemory<byte> encrypted = Aes256.GcmMode.Encrypt(data, key);
                        return encrypted;
                    }

                case PacketEncryptionMode.ChaCha20Poly1305:
                    {
                        byte[] nonce = CryptoKeyGen.CreateNonce();

                        // Encrypt using ChaCha20-Poly1305.
                        ChaCha20Poly1305.Encrypt(key, nonce, data.Span, null, out byte[] ciphertext, out byte[] tag);

                        // Combine nonce, ciphertext, and tag.
                        byte[] result = new byte[12 + ciphertext.Length + 16];
                        Buffer.BlockCopy(nonce, 0, result, 0, 12);
                        Buffer.BlockCopy(ciphertext, 0, result, 12, ciphertext.Length);
                        Buffer.BlockCopy(tag, 0, result, 12 + ciphertext.Length, 16);

                        return result;
                    }

                default:
                    throw new PackageException("The specified encryption algorithm is not supported.");
            }
        }
        catch (Exception ex)
        {
            throw new PackageException("Failed to encrypt data.", ex);
        }
    }

    /// <summary>
    /// Decrypts the provided data using the specified algorithm.
    /// </summary>
    /// <param name="data">The encrypted data as <see cref="ReadOnlyMemory{Byte}"/>.</param>
    /// <param name="key">The decryption key.</param>
    /// <param name="algorithm">
    /// The encryption algorithm that was used.
    /// (Example: Xtea, AesGcm, ChaCha20Poly1305)
    /// </param>
    /// <returns>The decrypted data as <see cref="ReadOnlyMemory{Byte}"/>.</returns>
    public static ReadOnlyMemory<byte> Decrypt(ReadOnlyMemory<byte> data, byte[] key, PacketEncryptionMode algorithm = PacketEncryptionMode.Xtea)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key), "Key cannot be null.");
        if (data.IsEmpty)
            throw new ArgumentException("Data cannot be empty.", nameof(data));

        try
        {
            switch (algorithm)
            {
                case PacketEncryptionMode.Xtea:
                    {
                        int bufferSize = data.Length + 7 & ~7;
                        Memory<byte> decryptedXtea = new byte[bufferSize];

                        bool success = Xtea.TryDecrypt(data, key.ConvertKey(), decryptedXtea);
                        if (!success)
                            throw new InternalErrorException("Authentication failed.");

                        return decryptedXtea;
                    }

                case PacketEncryptionMode.AesGcm:
                    {
                        ReadOnlyMemory<byte> decrypted = Aes256.GcmMode.Decrypt(data, key);
                        return decrypted;
                    }

                case PacketEncryptionMode.ChaCha20Poly1305:
                    {
                        ReadOnlySpan<byte> input = data.Span;
                        // Ensure the input has at least enough bytes for nonce (12 bytes) and tag (16 bytes)
                        if (input.Length < 12 + 16)
                            throw new ArgumentException("Invalid data length.", nameof(data));

                        ReadOnlySpan<byte> nonce = input[..12];
                        ReadOnlySpan<byte> tag = input.Slice(input.Length - 16, 16);
                        ReadOnlySpan<byte> ciphertext = input.Slice(12, input.Length - 12 - 16);

                        bool success = ChaCha20Poly1305.Decrypt(key, nonce, ciphertext, null, tag, out byte[] plaintext);
                        if (!success)
                            throw new PackageException("Authentication failed.");

                        return plaintext;
                    }

                default:
                    throw new PackageException("The specified encryption algorithm is not supported.");
            }
        }
        catch (Exception ex)
        {
            throw new PackageException("Failed to decrypt data.", ex);
        }
    }
}