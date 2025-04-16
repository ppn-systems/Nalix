using Notio.Common.Cryptography.Hashing;
using System;
using System.Text;

namespace Notio.Cryptography.Security;

/// <summary>
/// Provides a static helper class for deriving cryptographic keys using the PBKDF2 algorithm (RFC 2898).
/// Supports SHA-1 and SHA-256 as underlying hash functions.
/// </summary>
public static class RFC2898
{
    /// <summary>
    /// Derives a cryptographic key from the specified password and salt using PBKDF2 (RFC 2898).
    /// </summary>
    /// <param name="password">The password used to derive the key.</param>
    /// <param name="salt">The cryptographic salt as a byte array. Must not be null or empty.</param>
    /// <param name="iterations">The number of iterations to apply the hash function. Must be greater than 0.</param>
    /// <param name="keyLength">The desired length of the derived key in bytes. Must be greater than 0.</param>
    /// <param name="hashType">The hash algorithm to use (default is <see cref="HashAlgorithm.Sha1"/>).</param>
    /// <returns>A byte array containing the derived key.</returns>
    /// <exception cref="ArgumentException">Thrown if password or salt is null or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if iterations or keyLength is less than or equal to zero.</exception>
    public static byte[] DeriveKey(
        string password, byte[] salt,
        int iterations, int keyLength,
        HashAlgorithm hashType = HashAlgorithm.Sha1)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password cannot be null or empty.", nameof(password));
        if (salt == null || salt.Length == 0)
            throw new ArgumentException("Salt cannot be null or empty.", nameof(salt));
        if (iterations <= 0)
            throw new ArgumentOutOfRangeException(nameof(iterations), "Iterations must be greater than 0.");
        if (keyLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(keyLength), "Key length must be greater than 0.");

        using var pbkdf2 = new Pbkdf2(salt, iterations, keyLength, hashType);
        return pbkdf2.DeriveKey(password);
    }

    /// <summary>
    /// Derives a cryptographic key from the password and salt using PBKDF2 and returns the result as a Base64-encoded string.
    /// </summary>
    /// <param name="password">The password used to derive the key.</param>
    /// <param name="salt">The cryptographic salt as a byte array.</param>
    /// <param name="iterations">The number of iterations to apply the hash function.</param>
    /// <param name="keyLength">The desired length of the derived key in bytes.</param>
    /// <param name="hashType">The hash algorithm to use (default is <see cref="HashAlgorithm.Sha1"/>).</param>
    /// <returns>A Base64-encoded string representing the derived key.</returns>
    public static string DeriveKeyBase64(
        string password, byte[] salt,
        int iterations, int keyLength,
        HashAlgorithm hashType = HashAlgorithm.Sha1)
    {
        byte[] key = DeriveKey(password, salt, iterations, keyLength, hashType);
        return Convert.ToBase64String(key);
    }

    /// <summary>
    /// Derives a cryptographic key from the password and salt using PBKDF2 and returns the result as a lowercase hexadecimal string.
    /// </summary>
    /// <param name="password">The password used to derive the key.</param>
    /// <param name="salt">The cryptographic salt as a byte array.</param>
    /// <param name="iterations">The number of iterations to apply the hash function.</param>
    /// <param name="keyLength">The desired length of the derived key in bytes.</param>
    /// <param name="hashType">The hash algorithm to use (default is <see cref="HashAlgorithm.Sha1"/>).</param>
    /// <returns>A lowercase hexadecimal string representing the derived key.</returns>
    public static string DeriveKeyHex(
        string password, byte[] salt,
        int iterations, int keyLength,
        HashAlgorithm hashType = HashAlgorithm.Sha1)
    {
        byte[] key = DeriveKey(password, salt, iterations, keyLength, hashType);
        StringBuilder sb = new(key.Length * 2);

        foreach (byte b in key)
        {
            sb.Append(b.ToString("x2"));
        }

        return sb.ToString();
    }
}
