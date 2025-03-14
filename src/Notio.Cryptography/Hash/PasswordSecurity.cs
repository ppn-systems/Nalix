using Notio.Randomization;
using System;

namespace Notio.Cryptography.Hash;

/// <summary>
/// Provides secure password hashing and verification using PBKDF2.
/// </summary>
public static class PasswordSecurity
{
    /// <summary>
    /// Default salt size in bytes.
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
        using var pbkdf2 = new Pbkdf2(salt, Iterations, KeyLength, Pbkdf2.HashAlgorithmType.Sha256);
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
        using var pbkdf2 = new Pbkdf2(salt, Iterations, KeyLength, Pbkdf2.HashAlgorithmType.Sha256);
        return RandGenerator.ConstantTimeEquals(pbkdf2.DeriveKey(password), hash);
    }

    /// <summary>
    /// Hashes a password and returns a Base64-encoded string containing both the salt and hash.
    /// </summary>
    /// <param name="password">The plaintext password.</param>
    /// <returns>A Base64-encoded string of the salt and hash.</returns>
    public static string HashPasswordToBase64(string password)
    {
        HashPassword(password, out byte[] salt, out byte[] hash);
        return Convert.ToBase64String(salt) + ":" + Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Verifies a password against a Base64-encoded salt and hash.
    /// </summary>
    /// <param name="password">The password to verify.</param>
    /// <param name="encodedHash">The Base64-encoded salt and hash.</param>
    /// <returns><c>true</c> if the password matches; otherwise, <c>false</c>.</returns>
    public static bool VerifyPasswordFromBase64(string password, string encodedHash)
    {
        var parts = encodedHash.Split(':');
        if (parts.Length != 2) return false;

        byte[] salt = Convert.FromBase64String(parts[0]);
        byte[] storedHash = Convert.FromBase64String(parts[1]);

        return VerifyPassword(password, salt, storedHash);
    }
}
