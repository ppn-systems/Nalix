using System;
using System.Buffers;
using System.Security.Cryptography;

namespace Notio.Cryptography.Mode;

internal static class AesCfbCipher
{
    public static Aes256.MemoryBuffer Encrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> plaintext)
    {
        Aes256.ValidateKey(key);
        Aes256.ValidateInput(plaintext, nameof(plaintext));

        Span<byte> iv = stackalloc byte[Aes256.BlockSize];
        Aes256.GenerateSecureIV(iv);

        using var aes = CreateAesCFB(key, iv);
        IMemoryOwner<byte> resultOwner = MemoryPool<byte>.Shared.Rent(iv.Length + plaintext.Length);

        try
        {
            Span<byte> result = resultOwner.Memory.Span;

            iv.CopyTo(result);
            using ICryptoTransform encryptor = aes.CreateEncryptor();
            Span<byte> plaintextSpan = result[iv.Length..];

            plaintext.CopyTo(plaintextSpan);

            int paddingLength = Aes256.BlockSize - (plaintext.Length % Aes256.BlockSize);
            byte[] padding = new byte[paddingLength];
            new Random().NextBytes(padding); // Random padding for encryption

            plaintextSpan = plaintextSpan[..(plaintext.Length + paddingLength)];
            padding.CopyTo(plaintextSpan[plaintext.Length..]);

            unsafe
            {
                byte* buffer = stackalloc byte[Aes256.BlockSize];
                for (int i = 0; i < plaintextSpan.Length; i += Aes256.BlockSize)
                {
                    fixed (byte* inputPtr = plaintextSpan.Slice(i, Aes256.BlockSize))
                    {
                        encryptor.TransformBlock(
                            new Span<byte>(inputPtr, Aes256.BlockSize).ToArray(), 0, Aes256.BlockSize,
                            new Span<byte>(buffer, Aes256.BlockSize).ToArray(), 0
                        );
                    }

                    new Span<byte>(buffer, Aes256.BlockSize).CopyTo(plaintextSpan.Slice(i, Aes256.BlockSize));
                }
            }

            return new Aes256.MemoryBuffer(resultOwner, iv.Length + plaintextSpan.Length);
        }
        catch
        {
            resultOwner.Dispose();
            throw;
        }
    }

    public static Aes256.MemoryBuffer Decrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> ciphertext)
    {
        Aes256.ValidateKey(key);
        Aes256.ValidateInput(ciphertext, nameof(ciphertext));

        if (ciphertext.Length <= Aes256.BlockSize)
        {
            throw new ArgumentException("Ciphertext quá ngắn", nameof(ciphertext));
        }

        Span<byte> iv = stackalloc byte[Aes256.BlockSize];
        ciphertext[..Aes256.BlockSize].CopyTo(iv);

        using var aes = CreateAesCFB(key, iv);
        IMemoryOwner<byte> resultOwner = MemoryPool<byte>.Shared.Rent(ciphertext.Length - Aes256.BlockSize);

        try
        {
            Span<byte> result = resultOwner.Memory.Span;

            ReadOnlySpan<byte> encryptedSpan = ciphertext[Aes256.BlockSize..];
            encryptedSpan.CopyTo(result);

            using ICryptoTransform decryptor = aes.CreateDecryptor();

            Span<byte> buffer = stackalloc byte[Aes256.BlockSize];
            for (int i = 0; i < encryptedSpan.Length; i += Aes256.BlockSize)
            {
                int blockSize = Math.Min(Aes256.BlockSize, encryptedSpan.Length - i);

                decryptor.TransformBlock(
                    result.Slice(i, blockSize).ToArray(), 0,
                    blockSize, buffer.ToArray(), 0
                );

                buffer[..blockSize].CopyTo(result.Slice(i, blockSize));
            }

            // Remove padding
            int paddingLength = Aes256.BlockSize - (encryptedSpan.Length % Aes256.BlockSize);
            return new Aes256.MemoryBuffer(resultOwner, ciphertext.Length - Aes256.BlockSize - paddingLength);
        }
        catch
        {
            resultOwner.Dispose();
            throw;
        }
    }

    private static Aes CreateAesCFB(ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv)
    {
        Aes aes = Aes.Create();
        aes.Key = key.ToArray();
        aes.IV = iv.ToArray();
        aes.Mode = CipherMode.CFB;
        aes.Padding = PaddingMode.None;
        return aes;
    }
}