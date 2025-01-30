using System;
using System.Security.Cryptography;
using System.Text;

namespace Notio.Cryptography.Hash;

public static class SecurePasswordHasher
{
    private const int SaltSize = 16;  // Kích thước của Salt (16 bytes)

    /// <summary>
    /// Băm mật khẩu với Salt ngẫu nhiên.
    /// </summary>
    /// <param name="password">Mật khẩu cần băm.</param>
    /// <returns>Một chuỗi chứa Salt và Hash, phân tách bằng dấu ':'.</returns>
    public static string Hash(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password không được để trống.", nameof(password));

        // Tạo Salt ngẫu nhiên
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);

        // Kết hợp Salt và mật khẩu
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        byte[] combinedBytes = new byte[salt.Length + passwordBytes.Length];
        Buffer.BlockCopy(salt, 0, combinedBytes, 0, salt.Length);
        Buffer.BlockCopy(passwordBytes, 0, combinedBytes, salt.Length, passwordBytes.Length);

        // Tính toán Hash
        byte[] hashBytes = SHA256.HashData(combinedBytes);

        // Chuyển đổi sang chuỗi hex để lưu trữ
        string saltHex = Convert.ToHexString(salt);
        string hashHex = Convert.ToHexString(hashBytes);
        return $"{saltHex}:{hashHex}";
    }

    /// <summary>
    /// Xác minh mật khẩu so với giá trị Hash đã lưu trữ.
    /// </summary>
    /// <param name="password">Mật khẩu cần kiểm tra.</param>
    /// <param name="storedHash">Giá trị Hash đã lưu trữ.</param>
    /// <returns>True nếu mật khẩu khớp, ngược lại False.</returns>
    public static bool Verify(string password, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password không được để trống.", nameof(password));
        if (string.IsNullOrWhiteSpace(storedHash))
            throw new ArgumentException("StoredHash không được để trống.", nameof(storedHash));

        // Tách Salt và Hash từ chuỗi lưu trữ
        var parts = storedHash.Split(':');
        if (parts.Length != 2)
            throw new FormatException("Định dạng của storedHash không hợp lệ.");

        byte[] salt = Convert.FromHexString(parts[0]);
        byte[] storedHashBytes = Convert.FromHexString(parts[1]);

        // Kết hợp Salt và mật khẩu mới
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        byte[] combinedBytes = new byte[salt.Length + passwordBytes.Length];
        Buffer.BlockCopy(salt, 0, combinedBytes, 0, salt.Length);
        Buffer.BlockCopy(passwordBytes, 0, combinedBytes, salt.Length, passwordBytes.Length);

        // Tính toán Hash
        byte[] hashBytes = SHA256.HashData(combinedBytes);

        // So sánh Hash một cách an toàn
        return CryptographicOperations.FixedTimeEquals(hashBytes, storedHashBytes);
    }
}