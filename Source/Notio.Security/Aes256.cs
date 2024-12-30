using System;
using System.Buffers;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Notio.Security;

/// <summary>
/// Lớp cung cấp chức năng mã hóa và giải mã AES sử dụng một khóa.
/// </summary>
public static class Aes256
{

    /// <summary>
    /// Tạo một khóa AES 256-bit (32 byte).
    /// </summary>
    public static byte[] GenerateKey()
    {
        using Aes aes = Aes.Create();
        aes.KeySize = 256;
        aes.GenerateKey();
        return aes.Key;
    }

    /// <summary>
    /// Tăng giá trị của bộ đếm sử dụng cho mã hóa AES trong chế độ CTR.
    /// </summary>
    /// <param name="counter">Mảng byte bộ đếm để tăng giá trị.</param>
    internal static void IncrementCounter(byte[] counter)
    {
        for (int i = counter.Length - 1; i >= 0; i--)
        {
            if (++counter[i] != 0) break;
        }
    }

    /// <summary>
    /// Tạo một encryptor AES mới với khóa được cung cấp.
    /// </summary>
    /// <param name="key">Khóa sử dụng cho mã hóa AES.</param>
    /// <returns>Một instance mới của <see cref="Aes"/> encryptor.</returns>
    internal static Aes CreateAesEncryptor(byte[] key)
    {
        var aes = Aes.Create();
        aes.Key = key;
        aes.Mode = CipherMode.ECB;
        return aes;
    }

    /// <summary>
    /// Mã hóa văn bản được cung cấp sử dụng mã hóa AES trong chế độ CTR.
    /// </summary>
    /// <param name="key">Khóa mã hóa.</param>
    /// <param name="plaintext">Dữ liệu cần mã hóa.</param>
    /// <returns>Dữ liệu mã hóa.</returns>
    public static byte[] Encrypt(byte[] key, byte[] plaintext)
    {
        using var aes = CreateAesEncryptor(key);
        using var ms = new MemoryStream();
        byte[] counter = new byte[16];

        using var encryptor = aes.CreateEncryptor();
        byte[] encryptedCounter = ArrayPool<byte>.Shared.Rent(16);

        for (int i = 0; i < plaintext.Length; i += aes.BlockSize / 8)
        {
            encryptor.TransformBlock(counter, 0, counter.Length, encryptedCounter, 0);

            int bytesToEncrypt = Math.Min(plaintext.Length - i, aes.BlockSize / 8);
            byte[] block = new byte[bytesToEncrypt];
            Array.Copy(plaintext, i, block, 0, bytesToEncrypt);

            for (int j = 0; j < bytesToEncrypt; j++)
                block[j] ^= encryptedCounter[j];

            ms.Write(block, 0, bytesToEncrypt);
            IncrementCounter(counter);
        }

        ArrayPool<byte>.Shared.Return(encryptedCounter);
        return ms.ToArray();
    }

    /// <summary>
    /// Giải mã dữ liệu mã hóa sử dụng mã hóa AES trong chế độ CTR.
    /// </summary>
    /// <param name="key">Khóa giải mã.</param>
    /// <param name="ciphertext">Dữ liệu cần giải mã.</param>
    /// <returns>Văn bản đã giải mã.</returns>
    public static byte[] Decrypt(byte[] key, byte[] ciphertext)
    {
        using var aes = CreateAesEncryptor(key);
        using var ms = new MemoryStream(ciphertext);
        using var encryptor = aes.CreateEncryptor();

        byte[] counter = new byte[16];
        byte[] encryptedCounter = ArrayPool<byte>.Shared.Rent(16);

        using var resultStream = new MemoryStream();
        byte[] buffer = new byte[16];
        int bytesRead;

        while ((bytesRead = ms.Read(buffer, 0, buffer.Length)) > 0)
        {
            encryptor.TransformBlock(counter, 0, counter.Length, encryptedCounter, 0);

            for (int j = 0; j < bytesRead; j++)
                buffer[j] ^= encryptedCounter[j];

            resultStream.Write(buffer, 0, bytesRead);
            IncrementCounter(counter);
        }

        ArrayPool<byte>.Shared.Return(encryptedCounter);
        return resultStream.ToArray();
    }

    /// <summary>
    /// Mã hóa văn bản được cung cấp không đồng bộ sử dụng mã hóa AES trong chế độ CTR.
    /// </summary>
    /// <param name="key">Khóa mã hóa.</param>
    /// <param name="plaintext">Dữ liệu cần mã hóa.</param>
    /// <returns>Một task đại diện cho hoạt động mã hóa không đồng bộ, với dữ liệu mã hóa là kết quả.</returns>
    public static async ValueTask<byte[]> EncryptAsync(byte[] key, byte[] plaintext)
    {
        using var aes = Aes256.CreateAesEncryptor(key);
        byte[] iv = new byte[16];

        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(iv);
        }

        using var ms = new MemoryStream();
        await ms.WriteAsync(iv);

        byte[] counter = new byte[16];
        Array.Copy(iv, counter, iv.Length);
        using var encryptor = aes.CreateEncryptor();

        byte[] encryptedCounter = ArrayPool<byte>.Shared.Rent(16);

        for (int i = 0; i < plaintext.Length; i += aes.BlockSize / 8)
        {
            encryptor.TransformBlock(counter, 0, counter.Length, encryptedCounter, 0);

            int bytesToEncrypt = Math.Min(plaintext.Length - i, aes.BlockSize / 8);
            byte[] block = new byte[bytesToEncrypt];
            Array.Copy(plaintext, i, block, 0, bytesToEncrypt);

            for (int j = 0; j < bytesToEncrypt; j++)
                block[j] ^= encryptedCounter[j];

            await ms.WriteAsync(block.AsMemory(0, bytesToEncrypt));
            Aes256.IncrementCounter(counter);
        }

        ArrayPool<byte>.Shared.Return(encryptedCounter);
        return ms.ToArray();
    }

    /// <summary>
    /// Giải mã dữ liệu mã hóa không đồng bộ sử dụng mã hóa AES trong chế độ CTR.
    /// </summary>
    /// <param name="key">Khóa giải mã.</param>
    /// <param name="ciphertext">Dữ liệu cần giải mã.</param>
    /// <returns>Một task đại diện cho hoạt động giải mã không đồng bộ, với văn bản đã giải mã là kết quả.</returns>
    public static async ValueTask<byte[]> DecryptAsync(byte[] key, byte[] ciphertext)
    {
        using var aes = Aes256.CreateAesEncryptor(key);
        byte[] iv = new byte[16];
        Array.Copy(ciphertext, 0, iv, 0, iv.Length);

        using var ms = new MemoryStream(ciphertext, iv.Length, ciphertext.Length - iv.Length);
        using var encryptor = aes.CreateEncryptor();

        byte[] counter = new byte[16];
        Array.Copy(iv, counter, iv.Length);

        using var resultStream = new MemoryStream();
        byte[] buffer = new byte[16];
        int bytesRead;
        byte[] encryptedCounter = ArrayPool<byte>.Shared.Rent(16);

        while ((bytesRead = await ms.ReadAsync(buffer)) > 0)
        {
            encryptor.TransformBlock(counter, 0, counter.Length, encryptedCounter, 0);

            for (int j = 0; j < bytesRead; j++)
                buffer[j] ^= encryptedCounter[j];

            await resultStream.WriteAsync(buffer.AsMemory(0, bytesRead));
            Aes256.IncrementCounter(counter);
        }

        ArrayPool<byte>.Shared.Return(encryptedCounter);
        return resultStream.ToArray();
    }
}