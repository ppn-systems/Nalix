using System;
using System.Security.Cryptography;

namespace Notio.Cryptography.Mode;

internal class AesCtrMode
{
    public static ReadOnlyMemory<byte> Encrypt(ReadOnlyMemory<byte> plainText, ReadOnlyMemory<byte> key)
    {
        using var aes = Aes.Create();
        aes.Key = key.Span.ToArray();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;

        var counter = new byte[aes.BlockSize / 8]; 
        aes.GenerateIV(); 

        using var encryptor = aes.CreateEncryptor(aes.Key, null);

        var encrypted = new byte[plainText.Length];
        var buffer = new byte[aes.BlockSize / 8];

        int offset = 0;
        while (offset < plainText.Length)
        {
            encryptor.TransformBlock(counter, 0, counter.Length, buffer, 0);  

            int blockSize = Math.Min(aes.BlockSize / 8, plainText.Length - offset);
            for (int i = 0; i < blockSize; i++)
            {
                encrypted[offset + i] = (byte)(plainText.Span[offset + i] ^ buffer[i]);
            }

            IncrementCounter(counter);  

            offset += blockSize;
        }

        var result = new byte[counter.Length + encrypted.Length];
        counter.CopyTo(result, 0);
        encrypted.CopyTo(result, counter.Length);

        return result;
    }

    public static ReadOnlyMemory<byte> Decrypt(ReadOnlyMemory<byte> cipherText, ReadOnlyMemory<byte> key)
    {
        using var aes = Aes.Create();
        aes.Key = key.Span.ToArray();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;

        var counter = cipherText[..(aes.BlockSize / 8)].ToArray();
        var cipherData = cipherText[(aes.BlockSize / 8)..];

        using var decryptor = aes.CreateDecryptor(aes.Key, null);

        var decrypted = new byte[cipherData.Length];
        var buffer = new byte[aes.BlockSize / 8];

        int offset = 0;
        while (offset < cipherData.Length)
        {
            decryptor.TransformBlock(counter, 0, counter.Length, buffer, 0); 

            int blockSize = Math.Min(aes.BlockSize / 8, cipherData.Length - offset);
            for (int i = 0; i < blockSize; i++)
            {
                decrypted[offset + i] = (byte)(cipherData.Span[offset + i] ^ buffer[i]);
            }

            IncrementCounter(counter); 

            offset += blockSize;
        }

        return decrypted;
    }

    private static void IncrementCounter(byte[] counter)
    {
        for (int i = counter.Length - 1; i >= 0; i--)
            if (++counter[i] != 0) break;
    }
}