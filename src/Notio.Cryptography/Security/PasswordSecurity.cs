using Notio.Cryptography.Hashing;
using Notio.Randomization;
using System;

namespace Notio.Cryptography.Security;

/// <summary>
/// Provides secure password hashing and verification using PBKDF2.
/// </summary>
public static class PasswordSecurity
{
    /// <summary>
    /// Standard salt size in bytes.
    /// </summary>
    public const int SaltSize = 32;

    /// <summary>
    /// Derived key length in bytes.
    /// </summary>
    public const int KeyLength = 32;

    /// <summary>
    /// Number of iterations for PBKDF2.
    /// </summary>
    public const int Iterations = 100_000;

    /// <summary>
    /// Hashes a password using PBKDF2 and returns the salt and hash.
    /// </summary>
    /// <param name="password">The plaintext password to hash.</param>
    /// <param name="salt">The generated salt.</param>
    /// <param name="hash">The derived hash.</param>
    public static void HashPassword(string password, out byte[] salt, out byte[] hash)
    {
        salt = RandGenerator.GetBytes(SaltSize);
        using var pbkdf2 = new Pbkdf2(salt, Iterations, KeyLength, HashAlgorithm.Sha256);
        hash = pbkdf2.DeriveKey(password);
    }

    /// <summary>
    /// Verifies whether the provided password matches the stored hash.
    /// </summary>
    /// <param name="password">The password to verify.</param>
    /// <param name="salt">The salt used for hashing.</param>
    /// <param name="hash">The stored hash to compare against.</param>
    /// <returns><c>true</c> if the password is valid; otherwise, <c>false</c>.</returns>
    public static bool VerifyPassword(string password, byte[] salt, byte[] hash)
    {
        using var pbkdf2 = new Pbkdf2(salt, Iterations, KeyLength, HashAlgorithm.Sha256);
        return Pbkdf2.ConstantTimeEquals(pbkdf2.DeriveKey(password), hash);
    }

    /// <summary>
    /// Hashes a password and returns a Base64-encoded string with version, salt, and hash.
    /// Format: [version (1 byte)] + [salt] + [hash].
    /// </summary>
    /// <param name="password">The plaintext password.</param>
    /// <returns>A Base64-encoded string containing version, salt, and hash.</returns>
    public static string HashPasswordToBase64(string password)
    {
        HashPassword(password, out byte[] salt, out byte[] hash);
        byte[] combined = new byte[1 + salt.Length + hash.Length];
        byte version = 1;
        combined[0] = version;
        Array.Copy(salt, 0, combined, 1, salt.Length);
        Array.Copy(hash, 0, combined, 1 + salt.Length, hash.Length);
        return Convert.ToBase64String(combined);
    }

    /// <summary>
    /// Verifies a password against a Base64-encoded hash with version information.
    /// </summary>
    /// <param name="password">The password to verify.</param>
    /// <param name="encodedHash">The Base64-encoded string containing version, salt, and hash.</param>
    /// <returns><c>true</c> if the password matches; otherwise, <c>false</c>.</returns>
    public static bool VerifyPasswordFromBase64(string password, string encodedHash)
    {
        if (string.IsNullOrEmpty(encodedHash)) return false;

        try
        {
            byte[] combined = Convert.FromBase64String(encodedHash);
            if (combined.Length < 1 + SaltSize + KeyLength) return false;

            byte version = combined[0];
            byte[] salt = new byte[SaltSize];
            byte[] storedHash = new byte[KeyLength];
            Array.Copy(combined, 1, salt, 0, SaltSize);
            Array.Copy(combined, 1 + SaltSize, storedHash, 0, KeyLength);

            return version == 1 && VerifyPassword(password, salt, storedHash);
        }
        catch (FormatException)
        {
            return false; // Base64 không hợp lệ
        }
    }
}
