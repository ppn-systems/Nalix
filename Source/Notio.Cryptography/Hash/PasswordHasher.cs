using System;
using System.Security.Cryptography;
using System.Text;

namespace Notio.Cryptography.Hash;

public static class PasswordHasher
{
    public const int DefaultSaltLength = 64;
    public const int DefaultIterationCount = 100_000;
    public const int MinSaltLength = 8;
    public const int MaxPasswordLength = 1024;
    public const int KeyLength = 64;

    // Auto-generate salt if not provided
    public static byte[] GetRandomSalt(int saltLength = DefaultSaltLength)
        => RandomNumberGenerator.GetBytes(saltLength);

    public static string GetRandomSaltHexString(int saltLength = DefaultSaltLength)
        => Convert.ToHexString(GetRandomSalt(saltLength));

    public static bool ComparePasswordToHash(string password, byte[] storedHash, int iterations = DefaultIterationCount)
    {
        byte[] computedHash = HashPassword(password, iterations);
        return CryptographicOperations.FixedTimeEquals(computedHash, storedHash);
    }

    // If salt is not provided, it will automatically be generated
    public static byte[] HashPassword(string password, int iterations = DefaultIterationCount, byte[] salt = null)
    {
        salt ??= GetRandomSalt(); // Generate a random salt if it's not provided

        if (salt.Length < MinSaltLength)
            throw new ArgumentException($"Salt must be at least {MinSaltLength} bytes.");

        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        if (passwordBytes.Length > MaxPasswordLength)
            throw new ArgumentException($"Password must be at most {MaxPasswordLength} bytes.");

        return Rfc2898DeriveBytes.Pbkdf2(passwordBytes, salt, iterations, HashAlgorithmName.SHA512, KeyLength);
    }

    public static string HashPasswordHexString(string password, int iterations = DefaultIterationCount, byte[] salt = null)
        => Convert.ToHexString(HashPassword(password, iterations, salt));

    public static bool ValidatePasswordPolicy(
        string password, int minLength = 6,
        int maxLength = 128, int minUpper = 1,
        int minDigit = 1, int minSpecial = 1)
    {
        if (password.Length < minLength || password.Length > maxLength) return false;

        int upper = 0, digit = 0, special = 0;
        foreach (char c in password.AsSpan())
        {
            if (char.IsUpper(c)) upper++;
            else if (char.IsDigit(c)) digit++;
            else if (!char.IsLetterOrDigit(c)) special++;
        }

        return upper >= minUpper && digit >= minDigit && special >= minSpecial;
    }
}