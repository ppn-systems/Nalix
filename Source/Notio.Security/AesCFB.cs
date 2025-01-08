using System;
using System.Buffers;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Notio.Security;

public static class AesCFB
{
    

    public static async Task<byte[]> EncryptAsync(byte[] key, byte[] plaintext)
    {
        Aes256.ValidateKey(key);
        Aes256.ValidateInput(plaintext, nameof(plaintext));

        byte[] iv = Aes256.GenerateSecureIV();
        using var aes = CreateAesCFB(key, iv);

        // Pre-allocate buffer with known size to avoid resizing
        var resultSize = iv.Length + plaintext.Length;
        using var resultStream = new MemoryStream(resultSize);

        await resultStream.WriteAsync(iv);

        // Use ArrayPool to avoid unnecessary allocations
        byte[] buffer = ArrayPool<byte>.Shared.Rent(Aes256.BufferSize);
        try
        {
            using var encryptor = aes.CreateEncryptor();
            int position = 0;

            while (position < plaintext.Length)
            {
                int bytesToEncrypt = Math.Min(plaintext.Length - position, Aes256.BufferSize);
                Buffer.BlockCopy(plaintext, position, buffer, 0, bytesToEncrypt);

                encryptor.TransformBlock(buffer, 0, bytesToEncrypt, buffer, 0);
                await resultStream.WriteAsync(buffer.AsMemory(0, bytesToEncrypt));

                position += bytesToEncrypt;
            }

            return resultStream.ToArray();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public static async Task<byte[]> DecryptAsync(byte[] key, byte[] ciphertext)
    {
        Aes256.ValidateKey(key);
        Aes256.ValidateInput(ciphertext, nameof(ciphertext));

        if (ciphertext.Length <= Aes256.BlockSize)
        {
            throw new ArgumentException("Ciphertext quá ngắn", nameof(ciphertext));
        }

        byte[] iv = ciphertext.AsSpan(0, Aes256.BlockSize).ToArray();
        using var aes = CreateAesCFB(key, iv);

        // Pre-allocate result buffer
        var resultSize = ciphertext.Length - Aes256.BlockSize;
        using var resultStream = new MemoryStream(resultSize);

        // Use ArrayPool for buffer
        byte[] buffer = ArrayPool<byte>.Shared.Rent(Aes256.BufferSize);
        try
        {
            using var decryptor = aes.CreateDecryptor();
            int position = Aes256.BlockSize;

            while (position < ciphertext.Length)
            {
                int bytesToDecrypt = Math.Min(ciphertext.Length - position, Aes256.BufferSize);
                Buffer.BlockCopy(ciphertext, position, buffer, 0, bytesToDecrypt);

                decryptor.TransformBlock(buffer, 0, bytesToDecrypt, buffer, 0);
                await resultStream.WriteAsync(buffer.AsMemory(0, bytesToDecrypt));

                position += bytesToDecrypt;
            }

            return resultStream.ToArray();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
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