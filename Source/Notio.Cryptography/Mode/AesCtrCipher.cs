using System;
using System.Buffers;
using System.Security.Cryptography;

namespace Notio.Cryptography.Mode;

internal static class AesCtrCipher
{
    public static Aes256.MemoryBuffer Encrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> plaintext)
    {
        using var aes = CreateAesEncryptor(key);
        using var encryptor = aes.CreateEncryptor();

        var result = new byte[plaintext.Length];
        var counter = new byte[aes.BlockSize / 8];
        var encryptedCounter = new byte[counter.Length];

        for (int offset = 0; offset < plaintext.Length; offset += counter.Length)
        {
            // Encrypt counter
            encryptor.TransformBlock(counter, 0, counter.Length, encryptedCounter, 0);

            int bytesToEncrypt = Math.Min(plaintext.Length - offset, counter.Length);

            // XOR plaintext with encrypted counter
            for (int j = 0; j < bytesToEncrypt; j++)
                result[offset + j] = (byte)(plaintext[offset + j] ^ encryptedCounter[j]);

            IncrementCounter(counter);
        }

        return CreateMemoryBuffer(result);
    }

    public static Aes256.MemoryBuffer Decrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> ciphertext)
    {
        // Decryption is identical to encryption in CTR mode
        return Encrypt(key, ciphertext);
    }

    private static Aes256.MemoryBuffer CreateMemoryBuffer(byte[] result)
    {
        var memoryOwner = MemoryPool<byte>.Shared.Rent(result.Length);
        result.AsSpan().CopyTo(memoryOwner.Memory.Span);
        return new Aes256.MemoryBuffer(memoryOwner, result.Length);
    }

    private static Aes CreateAesEncryptor(ReadOnlySpan<byte> key)
    {
        var aes = Aes.Create();
        aes.Key = key.ToArray();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        return aes;
    }

    private static void IncrementCounter(byte[] counter)
    {
        for (int i = counter.Length - 1; i >= 0; i--)
        {
            if (++counter[i] != 0)
                break;
        }
    }
}