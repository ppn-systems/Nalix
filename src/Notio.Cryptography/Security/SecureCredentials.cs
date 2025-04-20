using Notio.Common.Cryptography.Hashing;
using Notio.Cryptography.Utilities;
using Notio.Randomization;
using System;

namespace Notio.Cryptography.Security;

/// <summary>
/// Provides secure secret hashing and verification using PBKDF2.
/// </summary>
public static class SecureCredentials
{
    #region Constants

    /// <summary>
    /// Standard key size in bytes.
    /// </summary>
    public const int KeySize = 32;

    /// <summary>
    /// Standard salt size in bytes.
    /// </summary>
    public const int SaltSize = 32;

    /// <summary>
    /// Number of iterations for PBKDF2.
    /// </summary>
    public const int Iterations = 100_000;

    #endregion

    #region Public Methods

    /// <summary>
    /// Hashes a secret using PBKDF2 and returns the salt and hash.
    /// </summary>
    /// <param name="secret">The plaintext secret to hash.</param>
    /// <param name="salt">The generated salt.</param>
    /// <param name="hash">The derived hash.</param>
    public static void HashPassword(string secret, out byte[] salt, out byte[] hash)
    {
        salt = RandGenerator.GetBytes(SaltSize);
        using PBKDF2 pbkdf2 = new(salt, Iterations, KeySize, HashAlgorithm.Sha256);
        hash = pbkdf2.DeriveKey(secret);
    }

    /// <summary>
    /// Verifies whether the provided secret matches the stored hash.
    /// </summary>
    /// <param name="secret">The secret to verify.</param>
    /// <param name="salt">The salt used for hashing.</param>
    /// <param name="hash">The stored hash to compare against.</param>
    /// <returns><c>true</c> if the secret is valid; otherwise, <c>false</c>.</returns>
    public static bool VerifyPassword(string secret, byte[] salt, byte[] hash)
    {
        using PBKDF2 pbkdf2 = new(salt, Iterations, KeySize, HashAlgorithm.Sha256);
        return BitwiseUtils.FixedTimeEquals(pbkdf2.DeriveKey(secret), hash);
    }

    /// <summary>
    /// Hashes a secret and returns a Base64-encoded string with version, salt, and hash.
    /// Format: [version (1 byte)] + [salt] + [hash].
    /// </summary>
    /// <param name="secret">The plaintext secret.</param>
    /// <returns>A Base64-encoded string containing version, salt, and hash.</returns>
    public static string HashPasswordToBase64(string secret)
    {
        HashPassword(secret, out byte[] salt, out byte[] hash);
        byte[] combined = new byte[1 + salt.Length + hash.Length];
        byte version = 1;
        combined[0] = version;
        Array.Copy(salt, 0, combined, 1, salt.Length);
        Array.Copy(hash, 0, combined, 1 + salt.Length, hash.Length);
        return Convert.ToBase64String(combined);
    }

    /// <summary>
    /// Verifies a secret against a Base64-encoded hash with version information.
    /// </summary>
    /// <param name="secret">The secret to verify.</param>
    /// <param name="encodedCredentials">The Base64-encoded string containing version, salt, and hash.</param>
    /// <returns><c>true</c> if the secret matches; otherwise, <c>false</c>.</returns>
    public static bool VerifyPasswordFromBase64(string secret, string encodedCredentials)
    {
        if (string.IsNullOrEmpty(encodedCredentials)) return false;

        try
        {
            byte[] combined = Convert.FromBase64String(encodedCredentials);
            if (combined.Length < 1 + SaltSize + KeySize) return false;

            byte version = combined[0];
            byte[] salt = new byte[SaltSize];
            byte[] storedHash = new byte[KeySize];
            Array.Copy(combined, 1, salt, 0, SaltSize);
            Array.Copy(combined, 1 + SaltSize, storedHash, 0, KeySize);

            return version == 1 && VerifyPassword(secret, salt, storedHash);
        }
        catch (FormatException)
        {
            return false; // Base64 không hợp lệ
        }
    }

    #endregion
}
