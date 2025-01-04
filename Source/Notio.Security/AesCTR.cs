using Notio.Security.Exceptions;
using System;
using System.Buffers;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Notio.Security;

/// <summary>
/// Lớp cung cấp chức năng mã hóa và giải mã AES-256 với chế độ CTR
/// </summary>
public static class AesCTR
{
    /// <summary>
    /// Tạo một khóa AES 256-bit mới
    /// </summary>
    public static byte[] GenerateKey()
    {
        try
        {
            using var aes = Aes.Create();
            aes.KeySize = Aes256.KeySize * 8; // Convert bytes to bits
            aes.GenerateKey();
            return aes.Key;
        }
        catch (Exception ex)
        {
            throw new CryptoOperationException("Failed to generate encryption key", ex);
        }
    }

    /// <summary>
    /// Mã hóa dữ liệu sử dụng AES-256 CTR mode
    /// </summary>
    public static byte[] Encrypt(byte[] key, byte[] plaintext)
    {
        Aes256.ValidateKey(key);
        Aes256.ValidateInput(plaintext, nameof(plaintext));

        byte[] encryptedCounter = null;
        byte[] counter = null;
        byte[] block = new byte[Aes256.BlockSize]; // Move allocation out of the loop

        try
        {
            encryptedCounter = Aes256.Pool.Rent(Aes256.BlockSize);
            counter = Aes256.Pool.Rent(Aes256.BlockSize);
            byte[] iv = Aes256.GenerateSecureIV();

            using var aes = CreateAesCTR(key);
            using var ms = new MemoryStream(plaintext.Length + Aes256.BlockSize);
            using var encryptor = aes.CreateEncryptor();

            ms.Write(iv);
            iv.CopyTo(counter, 0);

            for (int i = 0; i < plaintext.Length; i += Aes256.BlockSize)
            {
                encryptor.TransformBlock(counter, 0, Aes256.BlockSize, encryptedCounter, 0);

                int bytesToEncrypt = Math.Min(plaintext.Length - i, Aes256.BlockSize);
                plaintext.AsSpan(i, bytesToEncrypt).CopyTo(block);

                for (int j = 0; j < bytesToEncrypt; j++)
                    block[j] ^= encryptedCounter[j];

                ms.Write(block, 0, bytesToEncrypt);
                Aes256.IncrementCounter(counter);
            }

            return ms.ToArray();
        }
        catch (Exception ex) when (ex is not CryptoOperationException)
        {
            throw new CryptoOperationException("Encryption failed", ex);
        }
        finally
        {
            if (encryptedCounter != null) Aes256.Pool.Return(encryptedCounter);
            if (counter != null) Aes256.Pool.Return(counter);
        }
    }

    /// <summary>
    /// Giải mã dữ liệu đã mã hóa bằng AES-256 CTR mode
    /// </summary>
    public static byte[] Decrypt(byte[] key, byte[] ciphertext)
    {
        Aes256.ValidateKey(key);
        Aes256.ValidateInput(ciphertext, nameof(ciphertext));

        if (ciphertext.Length <= Aes256.BlockSize)
            throw new ArgumentException("Ciphertext is too short", nameof(ciphertext));

        byte[] encryptedCounter = null;
        byte[] counter = null;

        try
        {
            encryptedCounter = Aes256.Pool.Rent(Aes256.BlockSize);
            counter = Aes256.Pool.Rent(Aes256.BlockSize);

            // Lấy IV từ ciphertext
            Buffer.BlockCopy(ciphertext, 0, counter, 0, Aes256.BlockSize);

            using var aes = CreateAesCTR(key);
            using var ms = new MemoryStream(ciphertext, Aes256.BlockSize, ciphertext.Length - Aes256.BlockSize);
            using var encryptor = aes.CreateEncryptor();
            using var resultStream = new MemoryStream();

            byte[] buffer = new byte[Aes256.BlockSize];
            int bytesRead;

            while ((bytesRead = ms.Read(buffer, 0, Aes256.BlockSize)) > 0)
            {
                encryptor.TransformBlock(counter, 0, Aes256.BlockSize, encryptedCounter, 0);

                for (int j = 0; j < bytesRead; j++)
                    buffer[j] ^= encryptedCounter[j];

                resultStream.Write(buffer, 0, bytesRead);
                Aes256.IncrementCounter(counter);
            }

            return resultStream.ToArray();
        }
        catch (Exception ex) when (ex is not CryptoOperationException)
        {
            throw new CryptoOperationException("Decryption failed", ex);
        }
        finally
        {
            if (encryptedCounter != null) Aes256.Pool.Return(encryptedCounter);
            if (counter != null) Aes256.Pool.Return(counter);
        }
    }

    /// <summary>
    /// Mã hóa dữ liệu bất đồng bộ sử dụng AES-256 CTR mode
    /// </summary>
    public static async ValueTask<byte[]> EncryptAsync(byte[] key, byte[] plaintext)
    {
        Aes256.ValidateKey(key);
        Aes256.ValidateInput(plaintext, nameof(plaintext));

        byte[] encryptedCounter = null;
        byte[] counter = null;
        byte[] iv = null;
        byte[] block = new byte[Aes256.BlockSize]; // Move allocation out of the loop

        try
        {
            encryptedCounter = Aes256.Pool.Rent(Aes256.BlockSize);
            counter = Aes256.Pool.Rent(Aes256.BlockSize);
            iv = Aes256.GenerateSecureIV();

            using var aes = CreateAesCTR(key);
            using var ms = new MemoryStream(plaintext.Length + Aes256.BlockSize);
            using var encryptor = aes.CreateEncryptor();

            await ms.WriteAsync(iv);
            iv.CopyTo(counter, 0);

            for (int i = 0; i < plaintext.Length; i += Aes256.BlockSize)
            {
                encryptor.TransformBlock(counter, 0, Aes256.BlockSize, encryptedCounter, 0);

                int bytesToEncrypt = Math.Min(plaintext.Length - i, Aes256.BlockSize);
                plaintext.AsMemory(i, bytesToEncrypt).CopyTo(block);

                for (int j = 0; j < bytesToEncrypt; j++)
                    block[j] ^= encryptedCounter[j];

                await ms.WriteAsync(block.AsMemory(0, bytesToEncrypt));
                Aes256.IncrementCounter(counter);
            }

            return ms.ToArray();
        }
        catch (Exception ex) when (ex is not CryptoOperationException)
        {
            throw new CryptoOperationException("Async encryption failed", ex);
        }
        finally
        {
            if (encryptedCounter != null) Aes256.Pool.Return(encryptedCounter);
            if (counter != null) Aes256.Pool.Return(counter);
            if (iv != null) Aes256.Pool.Return(iv);
        }
    }

    /// <summary>
    /// Giải mã dữ liệu bất đồng bộ đã mã hóa bằng AES-256 CTR mode
    /// </summary>
    public static async ValueTask<byte[]> DecryptAsync(byte[] key, byte[] ciphertext)
    {
        Aes256.ValidateKey(key);
        Aes256.ValidateInput(ciphertext, nameof(ciphertext));

        if (ciphertext.Length <= Aes256.BlockSize)
            throw new ArgumentException("Ciphertext is too short", nameof(ciphertext));

        byte[] encryptedCounter = null;
        byte[] counter = null;

        try
        {
            encryptedCounter = Aes256.Pool.Rent(Aes256.BlockSize);
            counter = Aes256.Pool.Rent(Aes256.BlockSize);
            Buffer.BlockCopy(ciphertext, 0, counter, 0, Aes256.BlockSize);

            using var aes = CreateAesCTR(key);
            using var ms = new MemoryStream(ciphertext, Aes256.BlockSize, ciphertext.Length - Aes256.BlockSize);
            using var encryptor = aes.CreateEncryptor();
            using var resultStream = new MemoryStream();

            byte[] buffer = new byte[Aes256.BlockSize];
            int bytesRead;

            while ((bytesRead = await ms.ReadAsync(buffer.AsMemory(0, Aes256.BlockSize))) > 0)
            {
                encryptor.TransformBlock(counter, 0, Aes256.BlockSize, encryptedCounter, 0);

                for (int j = 0; j < bytesRead; j++)
                    buffer[j] ^= encryptedCounter[j];

                await resultStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                Aes256.IncrementCounter(counter);
            }

            return resultStream.ToArray();
        }
        catch (Exception ex) when (ex is not CryptoOperationException)
        {
            throw new CryptoOperationException("Async decryption failed", ex);
        }
        finally
        {
            if (encryptedCounter != null) Aes256.Pool.Return(encryptedCounter);
            if (counter != null) Aes256.Pool.Return(counter);
        }
    }

    /// <summary>
    /// Tạo và cấu hình AES encryptor
    /// </summary>
    private static Aes CreateAesCTR(byte[] key)
    {
        try
        {
            var aes = Aes.Create();
            aes.Key = key;
            aes.Mode = CipherMode.ECB;  // CTR mode uses ECB internally
            aes.Padding = PaddingMode.None;
            return aes;
        }
        catch (Exception ex)
        {
            throw new CryptoOperationException("Failed to create AES encryptor", ex);
        }
    }
}