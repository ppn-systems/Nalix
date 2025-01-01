using System;
using System.Buffers;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Notio.Security;

/// <summary>
/// Exception xảy ra khi có lỗi trong quá trình mã hóa/giải mã
/// </summary>
public class CryptoOperationException : Exception
{
    public CryptoOperationException(string message) : base(message) { }
    public CryptoOperationException(string message, Exception innerException)
        : base(message, innerException) { }
}

/// <summary>
/// Lớp cung cấp chức năng mã hóa và giải mã AES-256 với chế độ CTR
/// </summary>
public static class Aes256
{
    private const int BlockSize = 16;  // AES block size in bytes
    private const int KeySize = 32;    // AES-256 key size in bytes
    private static readonly ArrayPool<byte> Pool = ArrayPool<byte>.Shared;

    /// <summary>
    /// Kiểm tra tính hợp lệ của khóa
    /// </summary>
    private static void ValidateKey(byte[] key)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key), "Encryption key cannot be null");
        if (key.Length != KeySize)
            throw new ArgumentException($"Key must be {KeySize} bytes for AES-256", nameof(key));
    }

    /// <summary>
    /// Kiểm tra tính hợp lệ của dữ liệu đầu vào
    /// </summary>
    private static void ValidateInput(byte[] data, string paramName)
    {
        if (data == null)
            throw new ArgumentNullException(paramName, "Input data cannot be null");
        if (data.Length == 0)
            throw new ArgumentException("Input data cannot be empty", paramName);
    }

    /// <summary>
    /// Tạo một khóa AES 256-bit mới
    /// </summary>
    public static byte[] GenerateKey()
    {
        try
        {
            using var aes = Aes.Create();
            aes.KeySize = KeySize * 8; // Convert bytes to bits
            aes.GenerateKey();
            return aes.Key;
        }
        catch (Exception ex)
        {
            throw new CryptoOperationException("Failed to generate encryption key", ex);
        }
    }

    /// <summary>
    /// Tạo IV ngẫu nhiên an toàn
    /// </summary>
    private static byte[] GenerateSecureIV()
    {
        var iv = new byte[BlockSize];
        try
        {
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(iv);
            return iv;
        }
        catch (Exception ex)
        {
            throw new CryptoOperationException("Failed to generate secure IV", ex);
        }
    }

    /// <summary>
    /// Tăng bộ đếm CTR
    /// </summary>
    private static void IncrementCounter(Span<byte> counter)
    {
        for (int i = counter.Length - 1; i >= 0; i--)
        {
            if (++counter[i] != 0) break;
        }
    }

    /// <summary>
    /// Tạo và cấu hình AES encryptor
    /// </summary>
    private static Aes CreateAesEncryptor(byte[] key)
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

    /// <summary>
    /// Mã hóa dữ liệu sử dụng AES-256 CTR mode
    /// </summary>
    public static byte[] Encrypt(byte[] key, byte[] plaintext)
    {
        ValidateKey(key);
        ValidateInput(plaintext, nameof(plaintext));

        byte[] encryptedCounter = null;
        byte[] counter = null;
        byte[] iv = null;

        try
        {
            // Cấp phát bộ nhớ từ pool
            encryptedCounter = Pool.Rent(BlockSize);
            counter = Pool.Rent(BlockSize);
            iv = GenerateSecureIV();

            using var aes = CreateAesEncryptor(key);
            using var ms = new MemoryStream(plaintext.Length + BlockSize);
            using var encryptor = aes.CreateEncryptor();

            // Ghi IV vào đầu ciphertext
            ms.Write(iv);
            iv.CopyTo(counter, 0);

            // Mã hóa từng block
            for (int i = 0; i < plaintext.Length; i += BlockSize)
            {
                encryptor.TransformBlock(counter, 0, BlockSize, encryptedCounter, 0);

                int bytesToEncrypt = Math.Min(plaintext.Length - i, BlockSize);
                Span<byte> block = stackalloc byte[bytesToEncrypt];
                plaintext.AsSpan(i, bytesToEncrypt).CopyTo(block);

                // XOR với counter
                for (int j = 0; j < bytesToEncrypt; j++)
                    block[j] ^= encryptedCounter[j];

                ms.Write(block);
                IncrementCounter(counter);
            }

            return ms.ToArray();
        }
        catch (Exception ex) when (ex is not CryptoOperationException)
        {
            throw new CryptoOperationException("Encryption failed", ex);
        }
        finally
        {
            if (encryptedCounter != null) Pool.Return(encryptedCounter);
            if (counter != null) Pool.Return(counter);
        }
    }

    /// <summary>
    /// Giải mã dữ liệu đã mã hóa bằng AES-256 CTR mode
    /// </summary>
    public static byte[] Decrypt(byte[] key, byte[] ciphertext)
    {
        ValidateKey(key);
        ValidateInput(ciphertext, nameof(ciphertext));

        if (ciphertext.Length <= BlockSize)
            throw new ArgumentException("Ciphertext is too short", nameof(ciphertext));

        byte[] encryptedCounter = null;
        byte[] counter = null;

        try
        {
            encryptedCounter = Pool.Rent(BlockSize);
            counter = Pool.Rent(BlockSize);

            // Lấy IV từ ciphertext
            Buffer.BlockCopy(ciphertext, 0, counter, 0, BlockSize);

            using var aes = CreateAesEncryptor(key);
            using var ms = new MemoryStream(ciphertext, BlockSize, ciphertext.Length - BlockSize);
            using var encryptor = aes.CreateEncryptor();
            using var resultStream = new MemoryStream();

            byte[] buffer = new byte[BlockSize];
            int bytesRead;

            while ((bytesRead = ms.Read(buffer, 0, BlockSize)) > 0)
            {
                encryptor.TransformBlock(counter, 0, BlockSize, encryptedCounter, 0);

                for (int j = 0; j < bytesRead; j++)
                    buffer[j] ^= encryptedCounter[j];

                resultStream.Write(buffer, 0, bytesRead);
                IncrementCounter(counter);
            }

            return resultStream.ToArray();
        }
        catch (Exception ex) when (ex is not CryptoOperationException)
        {
            throw new CryptoOperationException("Decryption failed", ex);
        }
        finally
        {
            if (encryptedCounter != null) Pool.Return(encryptedCounter);
            if (counter != null) Pool.Return(counter);
        }
    }

    /// <summary>
    /// Mã hóa dữ liệu bất đồng bộ sử dụng AES-256 CTR mode
    /// </summary>
    public static async ValueTask<byte[]> EncryptAsync(byte[] key, byte[] plaintext)
    {
        ValidateKey(key);
        ValidateInput(plaintext, nameof(plaintext));

        byte[] encryptedCounter = null;
        byte[] counter = null;
        byte[] iv = null;

        try
        {
            encryptedCounter = Pool.Rent(BlockSize);
            counter = Pool.Rent(BlockSize);
            iv = GenerateSecureIV();

            using var aes = CreateAesEncryptor(key);
            using var ms = new MemoryStream(plaintext.Length + BlockSize);
            using var encryptor = aes.CreateEncryptor();

            await ms.WriteAsync(iv);
            iv.CopyTo(counter, 0);

            for (int i = 0; i < plaintext.Length; i += BlockSize)
            {
                encryptor.TransformBlock(counter, 0, BlockSize, encryptedCounter, 0);

                int bytesToEncrypt = Math.Min(plaintext.Length - i, BlockSize);
                Memory<byte> block = new byte[bytesToEncrypt];
                plaintext.AsMemory(i, bytesToEncrypt).CopyTo(block);

                for (int j = 0; j < bytesToEncrypt; j++)
                    block.Span[j] ^= encryptedCounter[j];

                await ms.WriteAsync(block);
                IncrementCounter(counter);
            }

            return ms.ToArray();
        }
        catch (Exception ex) when (ex is not CryptoOperationException)
        {
            throw new CryptoOperationException("Async encryption failed", ex);
        }
        finally
        {
            if (encryptedCounter != null) Pool.Return(encryptedCounter);
            if (counter != null) Pool.Return(counter);
            if (iv != null) Pool.Return(iv);
        }
    }

    /// <summary>
    /// Giải mã dữ liệu bất đồng bộ đã mã hóa bằng AES-256 CTR mode
    /// </summary>
    public static async ValueTask<byte[]> DecryptAsync(byte[] key, byte[] ciphertext)
    {
        ValidateKey(key);
        ValidateInput(ciphertext, nameof(ciphertext));

        if (ciphertext.Length <= BlockSize)
            throw new ArgumentException("Ciphertext is too short", nameof(ciphertext));

        byte[] encryptedCounter = null;
        byte[] counter = null;

        try
        {
            encryptedCounter = Pool.Rent(BlockSize);
            counter = Pool.Rent(BlockSize);
            Buffer.BlockCopy(ciphertext, 0, counter, 0, BlockSize);

            using var aes = CreateAesEncryptor(key);
            using var ms = new MemoryStream(ciphertext, BlockSize, ciphertext.Length - BlockSize);
            using var encryptor = aes.CreateEncryptor();
            using var resultStream = new MemoryStream();

            byte[] buffer = new byte[BlockSize];
            int bytesRead;

            while ((bytesRead = await ms.ReadAsync(buffer.AsMemory(0, BlockSize))) > 0)
            {
                encryptor.TransformBlock(counter, 0, BlockSize, encryptedCounter, 0);

                for (int j = 0; j < bytesRead; j++)
                    buffer[j] ^= encryptedCounter[j];

                await resultStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                IncrementCounter(counter);
            }

            return resultStream.ToArray();
        }
        catch (Exception ex) when (ex is not CryptoOperationException)
        {
            throw new CryptoOperationException("Async decryption failed", ex);
        }
        finally
        {
            if (encryptedCounter != null) Pool.Return(encryptedCounter);
            if (counter != null) Pool.Return(counter);
        }
    }
}