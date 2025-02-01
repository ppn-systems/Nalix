using System;
using System.Security.Cryptography;

namespace Notio.Cryptography.Ciphers.Symmetric.Mode;

internal static class AesCtrMode
{
    public static ReadOnlyMemory<byte> Encrypt(ReadOnlyMemory<byte> plainText, ReadOnlyMemory<byte> key)
    {
        if (plainText.IsEmpty)
            throw new ArgumentException("Plaintext cannot be empty", nameof(plainText));
        if (key.Length != 32)
            throw new ArgumentException("Key must be 32 bytes long", nameof(key));

        using Aes aes = Aes.Create();
        aes.Key = key.ToArray();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;

        Span<byte> nonce = stackalloc byte[Aes256.BlockSize];
        RandomNumberGenerator.Fill(nonce);

        using ICryptoTransform encryptor = aes.CreateEncryptor();
        Span<byte> encrypted = new byte[plainText.Length];
        Span<byte> counter = stackalloc byte[Aes256.BlockSize];
        Span<byte> keyStream = stackalloc byte[Aes256.BlockSize];
        nonce.CopyTo(counter);

        for (int offset = 0; offset < plainText.Length; offset += Aes256.BlockSize)
        {
            encryptor.TransformBlock(counter.ToArray(), 0, Aes256.BlockSize, keyStream.ToArray(), 0);
            int blockSize = Math.Min(Aes256.BlockSize, plainText.Length - offset);

            for (int i = 0; i < blockSize; i++)
                encrypted[offset + i] = (byte)(plainText.Span[offset + i] ^ keyStream[i]);

            IncrementCounter(counter);
        }

        byte[] result = new byte[nonce.Length + encrypted.Length];
        nonce.CopyTo(result);
        encrypted.CopyTo(result.AsSpan(nonce.Length));

        return result;
    }

    public static ReadOnlyMemory<byte> Decrypt(ReadOnlyMemory<byte> cipherText, ReadOnlyMemory<byte> key)
    {
        if (cipherText.Length < Aes256.BlockSize)
            throw new ArgumentException("Ciphertext is too short", nameof(cipherText));
        if (key.Length != 32)
            throw new ArgumentException("Key must be 32 bytes long", nameof(key));

        using Aes aes = Aes.Create();
        aes.Key = key.ToArray();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;

        ReadOnlySpan<byte> nonce = cipherText[..Aes256.BlockSize].Span;
        ReadOnlySpan<byte> cipherData = cipherText[Aes256.BlockSize..].Span;

        using ICryptoTransform decryptor = aes.CreateDecryptor();
        Span<byte> decrypted = new byte[cipherData.Length];
        Span<byte> counter = stackalloc byte[Aes256.BlockSize];
        Span<byte> keyStream = stackalloc byte[Aes256.BlockSize];
        nonce.CopyTo(counter);

        for (int offset = 0; offset < cipherData.Length; offset += Aes256.BlockSize)
        {
            decryptor.TransformBlock(counter.ToArray(), 0, Aes256.BlockSize, keyStream.ToArray(), 0);
            int blockSize = Math.Min(Aes256.BlockSize, cipherData.Length - offset);

            for (int i = 0; i < blockSize; i++)
                decrypted[offset + i] = (byte)(cipherData[offset + i] ^ keyStream[i]);

            IncrementCounter(counter);
        }

        return decrypted.ToArray();
    }

    private static void IncrementCounter(Span<byte> counter)
    {
        for (int i = counter.Length - 1; i >= 0; i--)
        {
            if (++counter[i] != 0)
                break;
        }
    }
}