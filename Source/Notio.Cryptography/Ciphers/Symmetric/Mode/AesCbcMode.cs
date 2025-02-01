using System;
using System.Security.Cryptography;

namespace Notio.Cryptography.Ciphers.Symmetric.Mode;

internal static class AesCbcMode
{
    private static Aes AesCbc(ReadOnlyMemory<byte> key)
    {
        var aesAlg = Aes.Create();
        aesAlg.Key = key.ToArray();
        aesAlg.Mode = CipherMode.CBC;
        aesAlg.Padding = PaddingMode.PKCS7;
        return aesAlg;
    }

    public static ReadOnlyMemory<byte> Encrypt(ReadOnlyMemory<byte> plainText, ReadOnlyMemory<byte> key)
    {
        if (plainText.IsEmpty)
            throw new ArgumentException("Plaintext cannot be empty", nameof(plainText));
        if (key.Length != Aes256.KeySize)
            throw new ArgumentException($"Key must be {Aes256.KeySize} bytes long", nameof(key));

        try
        {
            using var aesAlg = AesCbc(key);
            aesAlg.GenerateIV();

            using ICryptoTransform encryptor = aesAlg.CreateEncryptor();
            byte[] encryptedBytes = encryptor.TransformFinalBlock(plainText.ToArray(), 0, plainText.Length);

            byte[] result = new byte[Aes256.IvSize + encryptedBytes.Length];
            aesAlg.IV.CopyTo(result, 0);
            encryptedBytes.CopyTo(result, Aes256.IvSize);

            return result;
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException("Encryption failed", ex);
        }
    }

    public static ReadOnlyMemory<byte> Decrypt(ReadOnlyMemory<byte> cipherText, ReadOnlyMemory<byte> key)
    {
        if (cipherText.Length < Aes256.IvSize)
            throw new ArgumentException("Ciphertext too short", nameof(cipherText));
        if (key.Length != Aes256.KeySize)
            throw new ArgumentException($"Key must be {Aes256.KeySize} bytes long", nameof(key));

        try
        {
            using var aesAlg = AesCbc(key);

            byte[] iv = cipherText[..Aes256.IvSize].ToArray();
            byte[] encryptedData = cipherText[Aes256.IvSize..].ToArray();

            aesAlg.IV = iv;
            using ICryptoTransform decryptor = aesAlg.CreateDecryptor();

            return decryptor.TransformFinalBlock(encryptedData, 0, encryptedData.Length);
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException("Decryption failed", ex);
        }
    }
}