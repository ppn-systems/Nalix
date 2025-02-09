using System;
using System.Security.Cryptography;
using System.Text;

namespace Notio.Cryptography.Hash;

/// <summary>
/// Provides secure password hashing and validation using PBKDF2 (RFC 2898).
/// </summary>
public static class PasswordHasher
{
    /// <summary>
    /// Default salt length in bytes (64).
    /// </summary>
    public const int DefaultSaltLength = 64;

    /// <summary>
    /// Default iteration count for PBKDF2 (100,000).
    /// </summary>
    public const int DefaultIterationCount = 100_000;

    /// <summary>
    /// Minimum required salt length (8 bytes).
    /// </summary>
    public const int MinSaltLength = 8;

    /// <summary>
    /// Maximum allowed password length in bytes (1024).
    /// </summary>
    public const int MaxPasswordLength = 1024;

    /// <summary>
    /// Length of the derived key in bytes (64).
    /// </summary>
    public const int KeyLength = 64;

    /// <summary>
    /// Generates a cryptographically secure random salt.
    /// </summary>
    /// <param name="saltLength">Length of the salt in bytes (default: 64).</param>
    /// <returns>A byte array containing the generated salt.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="saltLength"/> is smaller than <see cref="MinSaltLength"/>.</exception>
    public static byte[] GenerateSalt(int saltLength = DefaultSaltLength)
    {
        if (saltLength < MinSaltLength)
            throw new ArgumentOutOfRangeException(nameof(saltLength), $"Salt length must be at least {MinSaltLength} bytes.");

        return RandomNumberGenerator.GetBytes(saltLength);
    }

    /// <summary>
    /// Generates a random salt and returns it as a hexadecimal string.
    /// </summary>
    /// <param name="saltLength">Length of the salt in bytes (default: 64).</param>
    /// <returns>A hex-encoded salt string.</returns>
    public static string GenerateSaltHex(int saltLength = DefaultSaltLength)
        => Convert.ToHexString(GenerateSalt(saltLength));

    /// <summary>
    /// Computes a PBKDF2 hash of the given password using a specified salt and iteration count.
    /// </summary>
    /// <param name="password">The password to hash.</param>
    /// <param name="salt">The salt used for hashing.</param>
    /// <param name="iterations">The number of iterations (default: 100,000).</param>
    /// <returns>The hashed password as a byte array.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="password"/> is empty, or <paramref name="salt"/> is too short.</exception>
    public static byte[] HashPassword(string password, byte[] salt, int iterations = DefaultIterationCount)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password cannot be null or empty.", nameof(password));

        if (salt == null || salt.Length < MinSaltLength)
            throw new ArgumentException($"Salt must be at least {MinSaltLength} bytes.", nameof(salt));

        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        if (passwordBytes.Length > MaxPasswordLength)
            throw new ArgumentException($"Password must be at most {MaxPasswordLength} bytes.", nameof(password));

        return Rfc2898DeriveBytes.Pbkdf2(passwordBytes, salt, iterations, HashAlgorithmName.SHA512, KeyLength);
    }

    /// <summary>
    /// Computes a PBKDF2 hash of the password and returns it as a hex-encoded string.
    /// </summary>
    /// <param name="password">The password to hash.</param>
    /// <param name="salt">The salt used for hashing.</param>
    /// <param name="iterations">The number of iterations (default: 100,000).</param>
    /// <returns>The hashed password as a hex string.</returns>
    public static string HashPasswordHex(string password, byte[] salt, int iterations = DefaultIterationCount)
        => Convert.ToHexString(HashPassword(password, salt, iterations));

    /// <summary>
    /// Compares a plaintext password with a stored hash to verify if they match.
    /// </summary>
    /// <param name="password">The plaintext password.</param>
    /// <param name="storedHash">The stored hashed password.</param>
    /// <param name="salt">The salt used for hashing.</param>
    /// <param name="iterations">The number of iterations used when hashing (default: 100,000).</param>
    /// <returns><c>true</c> if the password matches the stored hash; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="storedHash"/> is null or empty.</exception>
    public static bool VerifyPassword(string password, byte[] storedHash, byte[] salt, int iterations = DefaultIterationCount)
    {
        if (storedHash == null || storedHash.Length == 0)
            throw new ArgumentException("Stored hash cannot be null or empty.", nameof(storedHash));

        byte[] computedHash = HashPassword(password, salt, iterations);
        return CryptographicOperations.FixedTimeEquals(computedHash, storedHash);
    }

    /// <summary>
    /// Validates a password against security policies (length, uppercase, digits, special characters).
    /// </summary>
    /// <param name="password">The password to validate.</param>
    /// <param name="minLength">Minimum length required (default: 6).</param>
    /// <param name="maxLength">Maximum length allowed (default: 128).</param>
    /// <param name="minUpper">Minimum required uppercase letters (default: 1).</param>
    /// <param name="minDigit">Minimum required digits (default: 1).</param>
    /// <param name="minSpecial">Minimum required special characters (default: 1).</param>
    /// <returns><c>true</c> if the password meets the security requirements; otherwise, <c>false</c>.</returns>
    public static bool ValidatePasswordPolicy(
        string password, int minLength = 6, int maxLength = 128, int minUpper = 1, int minDigit = 1, int minSpecial = 1)
    {
        if (password.Length < minLength || password.Length > maxLength) return false;

        int upper = 0, digit = 0, special = 0;
        foreach (char c in password)
        {
            if (char.IsUpper(c)) upper++;
            else if (char.IsDigit(c)) digit++;
            else if (!char.IsLetterOrDigit(c)) special++;
        }

        return upper >= minUpper && digit >= minDigit && special >= minSpecial;
    }
}