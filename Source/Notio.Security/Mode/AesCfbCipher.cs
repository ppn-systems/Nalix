using System;
using System.Buffers;
using System.Security.Cryptography;

namespace Notio.Cryptography.Mode;

internal static class AesCfbCipher
{
    public static MemoryBuffer Encrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> plaintext)
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

            unsafe
            {
                byte* buffer = stackalloc byte[Aes256.BlockSize];
                for (int i = 0; i < plaintext.Length; i += Aes256.BlockSize)
                {
                    int blockSize = Math.Min(Aes256.BlockSize, plaintext.Length - i);

                    fixed (byte* inputPtr = plaintextSpan.Slice(i, blockSize))
                    {
                        encryptor.TransformBlock(
                            new Span<byte>(inputPtr, blockSize).ToArray(),
                            0,
                            blockSize,
                            new Span<byte>(buffer, blockSize).ToArray(), 
                            0
                        );
                    }

                    new Span<byte>(buffer, blockSize).CopyTo(plaintextSpan.Slice(i, blockSize));
                }
            }

            return new MemoryBuffer(resultOwner, iv.Length + plaintext.Length);
        }
        catch
        {
            resultOwner.Dispose();
            throw;
        }
    }

    public static MemoryBuffer Decrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> ciphertext)
    {
        Aes256.ValidateKey(key);
        Aes256.ValidateInput(ciphertext, nameof(ciphertext));

        if (ciphertext.Length <= Aes256.BlockSize)
        {
            throw new ArgumentException("Ciphertext quá ngắn", nameof(ciphertext));
        }

        // Lấy IV từ ciphertext
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
                    result.Slice(i, blockSize).ToArray(),
                    0,
                    blockSize,
                    buffer.ToArray(), 
                    0
                );

                buffer[..blockSize].CopyTo(result.Slice(i, blockSize));
            }

            return new MemoryBuffer(resultOwner, ciphertext.Length - Aes256.BlockSize);
        }
        catch
        {
            resultOwner.Dispose();
            throw;
        }
    }

    private static Aes CreateAesCFB(ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv)
    {
        var aes = Aes.Create();
        aes.Key = key.ToArray();
        aes.IV = iv.ToArray();
        aes.Mode = CipherMode.CFB;
        aes.Padding = PaddingMode.None;
        return aes;
    }
}