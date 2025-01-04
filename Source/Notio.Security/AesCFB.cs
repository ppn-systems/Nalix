using System;
using System.IO;
using System.Security.Cryptography;

namespace Notio.Security;

public static class AesCFB
{
    private const int BlockSize = 16;  // AES block size in bytes
    private const int KeySize = 32;    // AES-256 key size in bytes

    private static void ValidateKey(byte[] key)
    {
        if (key == null) throw new ArgumentNullException(nameof(key), "Encryption key cannot be null");
        if (key.Length != KeySize) throw new ArgumentException($"Key must be {KeySize} bytes for AES-256", nameof(key));
    }

    private static void ValidateInput(byte[] data, string paramName)
    {
        if (data == null) throw new ArgumentNullException(paramName, "Input data cannot be null");
        if (data.Length == 0) throw new ArgumentException("Input data cannot be empty", paramName);
    }

    private static byte[] GenerateSecureIV()
    {
        var iv = new byte[BlockSize];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(iv);
        }
        return iv;
    }

    private static Aes CreateAesEncryptor(byte[] key, byte[] iv)
    {
        var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CFB;
        aes.Padding = PaddingMode.None;
        return aes;
    }

    public static byte[] Encrypt(byte[] key, byte[] plaintext)
    {
        ValidateKey(key);
        ValidateInput(plaintext, nameof(plaintext));

        byte[] iv = GenerateSecureIV();
        using var aes = CreateAesEncryptor(key, iv);
        using var ms = new MemoryStream();
        using var encryptor = aes.CreateEncryptor();
        ms.Write(iv);

        byte[] buffer = new byte[BlockSize];
        for (int i = 0; i < plaintext.Length; i += BlockSize)
        {
            int bytesToEncrypt = Math.Min(plaintext.Length - i, BlockSize);
            plaintext.AsSpan(i, bytesToEncrypt).CopyTo(buffer);

            encryptor.TransformBlock(buffer, 0, bytesToEncrypt, buffer, 0);
            ms.Write(buffer, 0, bytesToEncrypt);
        }

        return ms.ToArray();
    }

    public static byte[] Decrypt(byte[] key, byte[] ciphertext)
    {
        ValidateKey(key);
        ValidateInput(ciphertext, nameof(ciphertext));

        if (ciphertext.Length <= BlockSize)
            throw new ArgumentException("Ciphertext is too short", nameof(ciphertext));

        byte[] iv = new byte[BlockSize];
        Buffer.BlockCopy(ciphertext, 0, iv, 0, BlockSize);

        using var aes = CreateAesEncryptor(key, iv);
        using var ms = new MemoryStream(ciphertext, BlockSize, ciphertext.Length - BlockSize);
        using var decryptor = aes.CreateDecryptor();
        byte[] buffer = new byte[BlockSize];
        using var resultStream = new MemoryStream();
        int bytesRead;
        while ((bytesRead = ms.Read(buffer, 0, BlockSize)) > 0)
        {
            decryptor.TransformBlock(buffer, 0, bytesRead, buffer, 0);
            resultStream.Write(buffer, 0, bytesRead);
        }

        return resultStream.ToArray();
    }
}