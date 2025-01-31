using System;
using System.Security.Cryptography;

namespace Notio.Cryptography.Ciphers.Symmetric.Mode;

internal static class AesGcmMode
{
    private static readonly int MaxTagSize = AesGcm.TagByteSizes.MaxSize;
    private static readonly int MaxNonceSize = AesGcm.NonceByteSizes.MaxSize;

    public static ReadOnlyMemory<byte> Encrypt(ReadOnlyMemory<byte> plainText, ReadOnlyMemory<byte> key)
    {
        if (plainText.IsEmpty)
            throw new ArgumentException("Plaintext cannot be empty", nameof(plainText));

        if (key.Length != 32) // 256-bit key
            throw new ArgumentException("Key must be 32 bytes long", nameof(key));

        try
        {
            using var aes = new AesGcm(key.Span, MaxTagSize);

            var nonce = new byte[MaxNonceSize];
            RandomNumberGenerator.Fill(nonce);

            var cipherText = new byte[plainText.Length];
            var tag = new byte[MaxTagSize];

            aes.Encrypt(nonce, plainText.Span, cipherText, tag);

            var result = new byte[nonce.Length + cipherText.Length + tag.Length];
            nonce.CopyTo(result, 0);
            cipherText.CopyTo(result, nonce.Length);
            tag.CopyTo(result, nonce.Length + cipherText.Length);

            return result;
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException("Encryption failed", ex);
        }
    }

    public static ReadOnlyMemory<byte> Decrypt(ReadOnlyMemory<byte> cipherText, ReadOnlyMemory<byte> key)
    {
        if (cipherText.Length < MaxNonceSize + MaxTagSize)
            throw new ArgumentException("Ciphertext is too short", nameof(cipherText));

        if (key.Length != 32) // 256-bit key
            throw new ArgumentException("Key must be 32 bytes long", nameof(key));

        try
        {
            using var aes = new AesGcm(key.Span, MaxTagSize);

            var nonce = cipherText[..MaxNonceSize].Span;
            var tag = cipherText[^MaxTagSize..].Span;
            var encryptedData = cipherText.Slice(MaxNonceSize, cipherText.Length - MaxNonceSize - MaxTagSize).Span;

            var decrypted = new byte[encryptedData.Length];
            aes.Decrypt(nonce, encryptedData, tag, decrypted);

            return decrypted;
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException("Decryption failed", ex);
        }
    }
}