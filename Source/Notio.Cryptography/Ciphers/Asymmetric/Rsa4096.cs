using System;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Notio.Cryptography.Ciphers.Asymmetric;

/// <summary>
/// Lớp cung cấp các chức năng mã hóa và giải mã sử dụng thuật toán RSA với khóa 4096 bit.
/// </summary>
public sealed class Rsa4096 : IDisposable
{
    private readonly RSA _rsa;

    /// <summary>
    /// Khởi tạo một đối tượng Rsa4096 mới và tạo cặp khóa RSA.
    /// </summary>
    /// <param name="keySize">Kích thước của khóa RSA (mặc định là 4096 bit).</param>
    public Rsa4096(int keySize = 4096)
    {
        if (keySize < 2048 || keySize > 16384)
            throw new ArgumentException("Key size must be between 2048 and 16384 bits.");

        _rsa = RSA.Create();
        _rsa.KeySize = keySize;
    }

    /// <summary>
    /// Xuất khóa công khai RSA.
    /// </summary>
    public RSAParameters PublicKey => _rsa.ExportParameters(false);

    /// <summary>
    /// Xuất khóa bí mật RSA.
    /// </summary>
    public RSAParameters PrivateKey => _rsa.ExportParameters(true);

    /// <summary>
    /// Nhập khóa công khai từ mảng byte.
    /// </summary>
    /// <param name="publicKeyBytes">Mảng byte chứa khóa công khai.</param>
    public void ImportPublicKey(byte[] publicKeyBytes)
    {
        if (publicKeyBytes == null || publicKeyBytes.Length == 0)
            throw new ArgumentException("Public key cannot be null or empty.");

        _rsa.ImportRSAPublicKey(new ReadOnlySpan<byte>(publicKeyBytes), out _);
    }

    /// <summary>
    /// Xuất khóa công khai dưới dạng mảng byte.
    /// </summary>
    /// <returns>Mảng byte chứa khóa công khai.</returns>
    public byte[] ExportPublicKey() => _rsa.ExportRSAPublicKey();

    /// <summary>
    /// Mã hóa văn bản bằng khóa công khai.
    /// </summary>
    /// <param name="plaintext">Mảng byte chứa dữ liệu cần mã hóa.</param>
    /// <returns>Mảng byte chứa dữ liệu đã được mã hóa.</returns>
    public byte[] Encrypt(byte[] plaintext)
    {
        if (plaintext == null || plaintext.Length == 0)
            throw new ArgumentException("Plaintext cannot be null or empty.");

        return _rsa.Encrypt(plaintext, RSAEncryptionPadding.OaepSHA256);
    }

    /// <summary>
    /// Giải mã dữ liệu đã mã hóa bằng khóa bí mật.
    /// </summary>
    /// <param name="ciphertext">Mảng byte chứa dữ liệu đã mã hóa.</param>
    /// <returns>Mảng byte đã được giải mã.</returns>
    public byte[] Decrypt(byte[] ciphertext)
    {
        if (ciphertext == null || ciphertext.Length == 0)
            throw new ArgumentException("Ciphertext cannot be null or empty.");

        return _rsa.Decrypt(ciphertext, RSAEncryptionPadding.OaepSHA256);
    }

    /// <summary>
    /// Mã hóa văn bản bất đồng bộ bằng khóa công khai.
    /// </summary>
    /// <param name="plaintext">Mảng byte chứa dữ liệu cần mã hóa.</param>
    /// <returns>Mảng byte chứa dữ liệu đã được mã hóa.</returns>
    public async Task<byte[]> EncryptAsync(byte[] plaintext)
    {
        if (plaintext == null || plaintext.Length == 0)
            throw new ArgumentException("Plaintext cannot be null or empty.");

        return await Task.Run(() => _rsa.Encrypt(plaintext, RSAEncryptionPadding.OaepSHA256));
    }

    /// <summary>
    /// Giải mã dữ liệu đã mã hóa bất đồng bộ bằng khóa bí mật.
    /// </summary>
    /// <param name="ciphertext">Mảng byte chứa dữ liệu đã mã hóa.</param>
    /// <returns>Mảng byte đã được giải mã.</returns>
    public async Task<byte[]> DecryptAsync(byte[] ciphertext)
    {
        if (ciphertext == null || ciphertext.Length == 0)
            throw new ArgumentException("Ciphertext cannot be null or empty.");

        return await Task.Run(() => _rsa.Decrypt(ciphertext, RSAEncryptionPadding.OaepSHA256));
    }

    /// <summary>
    /// Giải phóng tài nguyên được sử dụng bởi RSA.
    /// </summary>
    public void Dispose()
    {
        _rsa.Dispose();
        GC.SuppressFinalize(this);
    }
}