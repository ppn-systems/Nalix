using System;
using System.Buffers;
using System.IO;
using System.Security.Cryptography;

namespace Notio.Cryptography.Mode;

internal static unsafe class AesCtrCipher
{
    public static Aes256.MemoryBuffer Encrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> plaintext)
    {
        using var aes = CreateAesEncryptor(key);
        using var ms = new MemoryStream();
        byte[] counter = new byte[16];  // Khởi tạo counter với tất cả các byte bằng 0
        using var encryptor = aes.CreateEncryptor();
        byte[] encryptedCounter = ArrayPool<byte>.Shared.Rent(16);

        try
        {
            for (int i = 0; i < plaintext.Length; i += aes.BlockSize / 8)
            {
                // Mã hóa block counter và XOR với dữ liệu cần mã hóa
                encryptor.TransformBlock(counter, 0, counter.Length, encryptedCounter, 0);
                int bytesToEncrypt = Math.Min(plaintext.Length - i, aes.BlockSize / 8);
                byte[] block = new byte[bytesToEncrypt];
                plaintext.Slice(i, bytesToEncrypt).CopyTo(block);

                // XOR dữ liệu plaintext với encryptedCounter
                for (int j = 0; j < bytesToEncrypt; j++)
                    block[j] ^= encryptedCounter[j];

                ms.Write(block, 0, bytesToEncrypt);
                IncrementCounter(counter); // Cập nhật counter sau mỗi block
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(encryptedCounter);
        }

        var result = ms.ToArray();
        var memoryOwner = MemoryPool<byte>.Shared.Rent(result.Length);
        result.AsSpan().CopyTo(memoryOwner.Memory.Span);
        return new Aes256.MemoryBuffer(memoryOwner, result.Length);
    }

    public static Aes256.MemoryBuffer Decrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> ciphertext)
    {
        Console.WriteLine("Starting decryption...");

        using var aes = CreateAesEncryptor(key);
        using var ms = new MemoryStream(ciphertext.ToArray());
        using var encryptor = aes.CreateEncryptor();

        byte[] counter = new byte[16];  // Counter phải giống như khi mã hóa
        byte[] encryptedCounter = ArrayPool<byte>.Shared.Rent(16);

        try
        {
            using var resultStream = new MemoryStream();
            byte[] buffer = new byte[16];
            int bytesRead;

            while ((bytesRead = ms.Read(buffer, 0, buffer.Length)) > 0)
            {
                encryptor.TransformBlock(counter, 0, counter.Length, encryptedCounter, 0);
                Console.WriteLine($"Counter (Hex): {BitConverter.ToString(counter)}");
                Console.WriteLine($"Encrypted Counter (Hex): {BitConverter.ToString(encryptedCounter)}");

                // XOR dữ liệu mã hóa với encryptedCounter để giải mã
                for (int j = 0; j < bytesRead; j++)
                    buffer[j] ^= encryptedCounter[j];

                resultStream.Write(buffer, 0, bytesRead);
                IncrementCounter(counter); // Cập nhật counter sau mỗi block
            }

            var result = resultStream.ToArray();
            Console.WriteLine($"Decrypted Payload (Hex): {BitConverter.ToString(result)}");

            // Loại bỏ các byte `00` từ cuối dữ liệu
            int lastIndex = result.Length - 1;
            while (lastIndex >= 0 && result[lastIndex] == 0)
            {
                lastIndex--; // Loại bỏ các byte `00` từ cuối dữ liệu
            }

            // Nếu không có byte nào được cắt bỏ, chiều dài kết quả là toàn bộ mảng
            int actualLength = lastIndex + 1;

            // Chỉ sao chép phần dữ liệu thực tế, không bao gồm các byte `00` thừa
            var trimmedResult = new byte[actualLength];
            Array.Copy(result, trimmedResult, actualLength);

            var memoryOwner = MemoryPool<byte>.Shared.Rent(trimmedResult.Length);
            trimmedResult.AsSpan().CopyTo(memoryOwner.Memory.Span);
            return new Aes256.MemoryBuffer(memoryOwner, trimmedResult.Length);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(encryptedCounter);
        }
    }

    private static Aes CreateAesEncryptor(ReadOnlySpan<byte> key)
    {
        var aes = Aes.Create();
        aes.Key = key.ToArray();
        aes.Mode = CipherMode.ECB;  // AES phải sử dụng ECB mode trong chế độ CTR
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
