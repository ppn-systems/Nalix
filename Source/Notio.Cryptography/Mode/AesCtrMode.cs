using System;
using System.Linq;
using System.Security.Cryptography;

namespace Notio.Cryptography.Mode;

internal static class AesCtrMode
{
    public static ReadOnlyMemory<byte> Encrypt(ReadOnlyMemory<byte> plainText, ReadOnlyMemory<byte> key)
    {
        if (plainText.IsEmpty)
            throw new ArgumentException("Plaintext cannot be empty", nameof(plainText));
        if (key.Length != 32)
            throw new ArgumentException("Key must be 32 bytes long", nameof(key));

        try
        {
            using var aes = Aes.Create();
            aes.Key = key.ToArray();
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;

            var nonce = new byte[aes.BlockSize / 8];
            RandomNumberGenerator.Fill(nonce);

            Console.WriteLine($"Nonce (Encrypt): {BitConverter.ToString(nonce)}");

            using var encryptor = aes.CreateEncryptor();
            var encrypted = new byte[plainText.Length];
            var counter = (byte[])nonce.Clone();

            for (int offset = 0; offset < plainText.Length; offset += aes.BlockSize / 8)
            {
                var keyStream = new byte[aes.BlockSize / 8];
                encryptor.TransformBlock(counter, 0, counter.Length, keyStream, 0);
                int blockSize = Math.Min(aes.BlockSize / 8, plainText.Length - offset);

                for (int i = 0; i < blockSize; i++)
                {
                    encrypted[offset + i] = (byte)(plainText.Span[offset + i] ^ keyStream[i]);
                }

                Console.WriteLine($"Plaintext Block: {BitConverter.ToString(plainText.Span.Slice(offset, blockSize).ToArray())}");
                Console.WriteLine($"Ciphertext Block: {BitConverter.ToString(encrypted.Skip(offset).Take(blockSize).ToArray())}");

                IncrementCounter(counter);
            }

            var result = new byte[nonce.Length + encrypted.Length];
            nonce.CopyTo(result, 0);
            encrypted.CopyTo(result, nonce.Length);

            Console.WriteLine($"Encrypted Text Length: {result.Length}");
            return result;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Encryption failed", ex);
        }
    }

    public static ReadOnlyMemory<byte> Decrypt(ReadOnlyMemory<byte> cipherText, ReadOnlyMemory<byte> key)
    {
        if (cipherText.Length < Aes256.BlockSize)
            throw new ArgumentException("Ciphertext is too short", nameof(cipherText));
        if (key.Length != 32)
            throw new ArgumentException("Key must be 32 bytes long", nameof(key));

        try
        {
            using var aes = Aes.Create();
            aes.Key = key.ToArray();
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;

            var nonce = cipherText[..Aes256.BlockSize].ToArray();
            var cipherData = cipherText[Aes256.BlockSize..];

            Console.WriteLine($"Nonce (Decrypt): {BitConverter.ToString(nonce)}");

            using var decryptor = aes.CreateDecryptor();
            var decrypted = new byte[cipherData.Length];
            var counter = (byte[])nonce.Clone();

            for (int offset = 0; offset < cipherData.Length; offset += aes.BlockSize / 8)
            {
                var keyStream = new byte[aes.BlockSize / 8];
                decryptor.TransformBlock(counter, 0, counter.Length, keyStream, 0);
                int blockSize = Math.Min(aes.BlockSize / 8, cipherData.Length - offset);

                for (int i = 0; i < blockSize; i++)
                {
                    decrypted[offset + i] = (byte)(cipherData.Span[offset + i] ^ keyStream[i]);
                }

                Console.WriteLine($"Ciphertext Block: {BitConverter.ToString(cipherData.Span.Slice(offset, blockSize).ToArray())}");
                Console.WriteLine($"Decrypted Block: {BitConverter.ToString(decrypted.Skip(offset).Take(blockSize).ToArray())}");

                IncrementCounter(counter);
            }

            Console.WriteLine($"Decrypted Text Length: {decrypted.Length}");
            return decrypted;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Decryption failed", ex);
        }
    }

    private static void IncrementCounter(byte[] counter)
    {
        for (int i = counter.Length - 1; i >= 0; i--)
        {
            counter[i]++;
            if (counter[i] != 0)
                break;
        }
    }
}