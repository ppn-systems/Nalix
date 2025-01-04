using System;
using System.IO;
using System.Security.Cryptography;

namespace Notio.Security;

public static class AesCFB
{
    public static byte[] Encrypt(byte[] key, byte[] plaintext)
    {
        Aes256.ValidateKey(key);
        Aes256.ValidateInput(plaintext, nameof(plaintext));

        byte[] iv = Aes256.GenerateSecureIV();
        using var aes = CreateAesCFB(key, iv);
        using var ms = new MemoryStream();
        using var encryptor = aes.CreateEncryptor();
        ms.Write(iv);

        byte[] buffer = new byte[Aes256.BlockSize];
        for (int i = 0; i < plaintext.Length; i += Aes256.BlockSize)
        {
            int bytesToEncrypt = Math.Min(plaintext.Length - i, Aes256.BlockSize);
            plaintext.AsSpan(i, bytesToEncrypt).CopyTo(buffer);

            encryptor.TransformBlock(buffer, 0, bytesToEncrypt, buffer, 0);
            ms.Write(buffer, 0, bytesToEncrypt);
        }

        return ms.ToArray();
    }

    public static byte[] Decrypt(byte[] key, byte[] ciphertext)
    {
        Aes256.ValidateKey(key);
        Aes256.ValidateInput(ciphertext, nameof(ciphertext));

        if (ciphertext.Length <= Aes256.BlockSize)
            throw new ArgumentException("Ciphertext is too short", nameof(ciphertext));

        byte[] iv = new byte[Aes256.BlockSize];
        Buffer.BlockCopy(ciphertext, 0, iv, 0, Aes256.BlockSize);

        using var aes = CreateAesCFB(key, iv);
        using var ms = new MemoryStream(ciphertext, Aes256.BlockSize, ciphertext.Length - Aes256.BlockSize);
        using var decryptor = aes.CreateDecryptor();
        byte[] buffer = new byte[Aes256.BlockSize];
        using var resultStream = new MemoryStream();
        int bytesRead;
        while ((bytesRead = ms.Read(buffer, 0, Aes256.BlockSize)) > 0)
        {
            decryptor.TransformBlock(buffer, 0, bytesRead, buffer, 0);
            resultStream.Write(buffer, 0, bytesRead);
        }

        return resultStream.ToArray();
    }

    private static Aes CreateAesCFB(byte[] key, byte[] iv)
    {
        var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CFB;
        aes.Padding = PaddingMode.None;
        return aes;
    }
}