using System;
using System.Security.Cryptography;

namespace Notio.Cryptography.Mode;

public class AesCbcMode
{
    public static ReadOnlyMemory<byte> Encrypt(ReadOnlyMemory<byte> plainText, ReadOnlyMemory<byte> key)
    {
        using var aesAlg = Aes.Create();
        aesAlg.Key = key.Span.ToArray();
        aesAlg.GenerateIV();
        aesAlg.Mode = CipherMode.CBC;
        aesAlg.Padding = PaddingMode.PKCS7;

        using var encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);
        var encrypted = new byte[aesAlg.IV.Length + plainText.Length];

        aesAlg.IV.CopyTo(encrypted, 0);
        var encryptedBytes = encryptor.TransformFinalBlock(plainText.Span.ToArray(), 0, plainText.Length);
        encryptedBytes.CopyTo(encrypted, aesAlg.IV.Length);

        return encrypted;
    }

    public static ReadOnlyMemory<byte> Decrypt(ReadOnlyMemory<byte> cipherText, ReadOnlyMemory<byte> key)
    {
        using var aesAlg = Aes.Create();
        aesAlg.Key = key.Span.ToArray();
        aesAlg.Mode = CipherMode.CBC;
        aesAlg.Padding = PaddingMode.PKCS7;

        var iv = cipherText[..(aesAlg.BlockSize / 8)].Span;
        aesAlg.IV = iv.ToArray();

        using var decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

        var decrypted = new byte[cipherText.Length - aesAlg.BlockSize / 8];

        decryptor.TransformBlock(cipherText[(aesAlg.BlockSize / 8)..].Span.ToArray(), 0, cipherText.Length - aesAlg.BlockSize / 8, decrypted, 0);

        return decrypted;
    }
}