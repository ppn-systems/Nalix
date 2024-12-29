using System;
using System.Security.Cryptography;
using System.Text;

namespace Notio.Security;

/// <summary>
/// Lớp cung cấp các chức năng mã hóa và giải mã sử dụng thuật toán RSA với khóa 4096 bit.
/// </summary>
public static class Rsa4096
{
    /// <summary>
    /// Tạo và trả về một cặp khóa RSA mới.
    /// </summary>
    /// <param name="keySize">Kích thước của khóa RSA (mặc định là 4096 bit).</param>
    public static (RSAParameters PublicKey, RSAParameters PrivateKey) GenerateKeys(int keySize = 4096)
    {
        using var rsa = RSA.Create();
        rsa.KeySize = keySize;
        return (rsa.ExportParameters(false), rsa.ExportParameters(true));
    }

    /// <summary>
    /// Mã hóa văn bản bằng khóa công khai.
    /// </summary>
    /// <param name="publicKey">Khóa công khai RSA.</param>
    /// <param name="plaintext">Chuỗi văn bản cần mã hóa.</param>
    /// <returns>Mảng byte chứa dữ liệu đã được mã hóa.</returns>
    public static byte[] Encrypt(RSAParameters publicKey, string plaintext)
    {
        using var rsaEncryptor = RSA.Create();
        rsaEncryptor.ImportParameters(publicKey);

        // Sử dụng OAEP padding thay cho PKCS1
        return rsaEncryptor.Encrypt(Encoding.UTF8.GetBytes(plaintext), RSAEncryptionPadding.OaepSHA256);
    }

    /// <summary>
    /// Giải mã dữ liệu đã mã hóa bằng khóa bí mật.
    /// </summary>
    /// <param name="privateKey">Khóa bí mật RSA.</param>
    /// <param name="ciphertext">Mảng byte chứa dữ liệu đã mã hóa.</param>
    /// <returns>Chuỗi văn bản đã được giải mã.</returns>
    public static string Decrypt(RSAParameters privateKey, byte[] ciphertext)
    {
        using var rsaDecryptor = RSA.Create();
        rsaDecryptor.ImportParameters(privateKey);
        return Encoding.UTF8.GetString(rsaDecryptor.Decrypt(ciphertext, RSAEncryptionPadding.OaepSHA256));
    }
}