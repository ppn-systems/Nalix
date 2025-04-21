using Nalix.Common.Cryptography.Hashing;
using Nalix.Cryptography.Utilities;
using Nalix.Randomization;
using System;

namespace Nalix.Cryptography.Security;

/// <summary>
/// Provides secure credential hashing and verification using PBKDF2.
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
    /// Generates a hash for a credential using PBKDF2 and returns the salt and hash.
    /// </summary>
    /// <param name="credential">The plaintext credential to hash.</param>
    /// <param name="salt">The generated salt.</param>
    /// <param name="hash">The derived hash.</param>
    public static void GenerateCredentialHash(string credential, out byte[] salt, out byte[] hash)
    {
        salt = RandGenerator.GetBytes(SaltSize);
        using PBKDF2 pbkdf2 = new(salt, Iterations, KeySize, HashAlgorithm.Sha256);
        hash = pbkdf2.DeriveKey(credential);
    }

    /// <summary>
    /// Generates a Base64-encoded string containing version, salt, and hash for a credential.
    /// Format: [version (1 byte)] + [salt] + [hash].
    /// </summary>
    /// <param name="credential">The plaintext credential.</param>
    /// <returns>A Base64-encoded string containing version, salt, and hash.</returns>
    public static string GenerateCredentialBase64(string credential)
    {
        GenerateCredentialHash(credential, out byte[] salt, out byte[] hash);
        byte[] combined = new byte[1 + salt.Length + hash.Length];
        byte version = 1;
        combined[0] = version;
        Array.Copy(salt, 0, combined, 1, salt.Length);
        Array.Copy(hash, 0, combined, 1 + salt.Length, hash.Length);
        return Convert.ToBase64String(combined);
    }

    /// <summary>
    /// Verifies whether the provided credential matches the stored hash.
    /// </summary>
    /// <param name="credential">The credential to verify.</param>
    /// <param name="salt">The salt used for hashing.</param>
    /// <param name="hash">The stored hash to compare against.</param>
    /// <returns><c>true</c> if the credential is valid; otherwise, <c>false</c>.</returns>
    public static bool VerifyCredentialHash(string credential, byte[] salt, byte[] hash)
    {
        using PBKDF2 pbkdf2 = new(salt, Iterations, KeySize, HashAlgorithm.Sha256);
        return BitwiseUtils.FixedTimeEquals(pbkdf2.DeriveKey(credential), hash);
    }

    /// <summary>
    /// Verifies a credential against a Base64-encoded hash with version information.
    /// </summary>
    /// <param name="credential">The credential to verify.</param>
    /// <param name="encodedCredentials">The Base64-encoded string containing version, salt, and hash.</param>
    /// <returns><c>true</c> if the credential matches; otherwise, <c>false</c>.</returns>
    public static bool VerifyCredentialFromBase64(string credential, string encodedCredentials)
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

            return version == 1 && VerifyCredentialHash(credential, salt, storedHash);
        }
        catch (FormatException)
        {
            return false; // Base64 không hợp lệ
        }
    }

    #endregion
}
