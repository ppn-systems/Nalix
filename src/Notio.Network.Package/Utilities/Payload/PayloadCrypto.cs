using Notio.Common.Exceptions;
using Notio.Cryptography.Ciphers;
using Notio.Cryptography.Ciphers.Symmetric;
using Notio.Cryptography.Ciphers.Symmetric.Enums;
using System;
using System.Buffers;

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
    public static Memory<byte> Encrypt(Memory<byte> data, byte[] key, EncryptionMode algorithm = EncryptionMode.Xtea)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key), "Key cannot be null.");
        if (data.IsEmpty)
            throw new ArgumentException("Data cannot be empty.", nameof(data));

        try
        {
            switch (algorithm)
            {
                case EncryptionMode.Xtea:
                    {
                        int bufferSize = (data.Length + 7) & ~7;
                        byte[] encryptedXtea = ArrayPool<byte>.Shared.Rent(bufferSize);

                        try
                        {
                            Xtea.Encrypt(data, CryptoKeyGen.ConvertKey(key), encryptedXtea.AsMemory(0, bufferSize));
                            return encryptedXtea.AsMemory(0, bufferSize);
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(encryptedXtea);
                        }
                    }

                case EncryptionMode.AesGcm:
                    return Aes256.GcmMode.Encrypt(data, key);

                case EncryptionMode.ChaCha20Poly1305:
                    {
                        Span<byte> nonce = CryptoKeyGen.CreateNonce();

                        ChaCha20Poly1305.Encrypt(key, nonce, data.Span, null, out byte[] ciphertext, out byte[] tag);

                        byte[] result = new byte[12 + ciphertext.Length + 16];
                        nonce.CopyTo(result);
                        ciphertext.CopyTo(result.AsSpan(12));
                        tag.CopyTo(result.AsSpan(12 + ciphertext.Length));

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
    public static Memory<byte> Decrypt(Memory<byte> data, byte[] key, EncryptionMode algorithm = EncryptionMode.Xtea)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key), "Key cannot be null.");
        if (data.IsEmpty)
            throw new ArgumentException("Data cannot be empty.", nameof(data));

        try
        {
            switch (algorithm)
            {
                case EncryptionMode.Xtea:
                    {
                        int bufferSize = (data.Length + 7) & ~7;
                        byte[] decryptedXtea = ArrayPool<byte>.Shared.Rent(bufferSize);

                        try
                        {
                            if (!Xtea.TryDecrypt(data, CryptoKeyGen.ConvertKey(key), decryptedXtea.AsMemory(0, bufferSize)))
                                throw new InternalErrorException("Authentication failed.");

                            return decryptedXtea.AsMemory(0, bufferSize);
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(decryptedXtea);
                        }
                    }

                case EncryptionMode.AesGcm:
                    return Aes256.GcmMode.Decrypt(data, key);

                case EncryptionMode.ChaCha20Poly1305:
                    {
                        ReadOnlySpan<byte> input = data.Span;
                        if (input.Length < 28) // Min size = 12 (nonce) + 16 (tag)
                            throw new ArgumentException("Invalid data length.", nameof(data));

                        ReadOnlySpan<byte> nonce = input[..12];
                        ReadOnlySpan<byte> tag = input.Slice(input.Length - 16, 16);
                        ReadOnlySpan<byte> ciphertext = input[12..^16];

                        if (!ChaCha20Poly1305.Decrypt(key, nonce, ciphertext, null, tag, out byte[] plaintext))
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