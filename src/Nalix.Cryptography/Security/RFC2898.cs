// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Security.Enums;

namespace Nalix.Cryptography.Security;

/// <summary>
/// Provides a static helper class for deriving cryptographic keys using the PBKDF2 algorithm (RFC 2898).
/// Supports SHA-1 and SHA-256 as underlying hash functions.
/// </summary>
public static class RFC2898
{
    #region Public Methods

    /// <summary>
    /// Derives a cryptographic key from the specified password and salt using PBKDF2 (RFC 2898).
    /// </summary>
    /// <param name="password">The password used to derive the key.</param>
    /// <param name="salt">The cryptographic salt as a byte array. Must not be null or empty.</param>
    /// <param name="iterations">The number of iterations to apply the hash function. Must be greater than 0.</param>
    /// <param name="keyLength">The desired length of the derived key in bytes. Must be greater than 0.</param>
    /// <param name="hashType">The hash algorithm to use (default is <see cref="HashAlgorithmType.Sha1"/>).</param>
    /// <returns>A byte array containing the derived key.</returns>
    /// <exception cref="System.ArgumentException">Thrown if password or salt is null or empty.</exception>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown if iterations or keyLength is less than or equal to zero.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Byte[] DeriveKey(
        System.String password, System.Byte[] salt,
        System.Int32 iterations, System.Int32 keyLength,
        HashAlgorithmType hashType = HashAlgorithmType.Sha1)
    {
        if (System.String.IsNullOrEmpty(password))
        {
            throw new System.ArgumentException("Password cannot be null or empty.", nameof(password));
        }

        if (salt == null || salt.Length == 0)
        {
            throw new System.ArgumentException("Salt cannot be null or empty.", nameof(salt));
        }

        if (iterations <= 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(iterations), "Iterations must be greater than 0.");
        }

        if (keyLength <= 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(keyLength), "Key length must be greater than 0.");
        }

        using var pbkdf2 = new PBKDF2(salt, iterations, keyLength, hashType);
        return pbkdf2.DeriveKey(password);
    }

    /// <summary>
    /// Derives a cryptographic key from the password and salt using PBKDF2 and returns the result as a Base64Value-encoded string.
    /// </summary>
    /// <param name="password">The password used to derive the key.</param>
    /// <param name="salt">The cryptographic salt as a byte array.</param>
    /// <param name="iterations">The number of iterations to apply the hash function.</param>
    /// <param name="keyLength">The desired length of the derived key in bytes.</param>
    /// <param name="hashType">The hash algorithm to use (default is <see cref="HashAlgorithmType.Sha1"/>).</param>
    /// <returns>A Base64Value-encoded string representing the derived key.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.String DeriveKeyBase64(
        System.String password, System.Byte[] salt,
        System.Int32 iterations, System.Int32 keyLength,
        HashAlgorithmType hashType = HashAlgorithmType.Sha1)
    {
        System.Byte[] key = DeriveKey(password, salt, iterations, keyLength, hashType);
        return System.Convert.ToBase64String(key);
    }

    /// <summary>
    /// Derives a cryptographic key from the password and salt using PBKDF2 and returns the result as a lowercase hexadecimal string.
    /// </summary>
    /// <param name="password">The password used to derive the key.</param>
    /// <param name="salt">The cryptographic salt as a byte array.</param>
    /// <param name="iterations">The number of iterations to apply the hash function.</param>
    /// <param name="keyLength">The desired length of the derived key in bytes.</param>
    /// <param name="hashType">The hash algorithm to use (default is <see cref="HashAlgorithmType.Sha1"/>).</param>
    /// <returns>A lowercase hexadecimal string representing the derived key.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.String DeriveKeyHex(
        System.String password, System.Byte[] salt,
        System.Int32 iterations, System.Int32 keyLength,
        HashAlgorithmType hashType = HashAlgorithmType.Sha1)
    {
        System.Byte[] key = DeriveKey(password, salt, iterations, keyLength, hashType);
        System.Text.StringBuilder sb = new(key.Length * 2);

        foreach (System.Byte b in key)
        {
            _ = sb.Append(b.ToString("x2"));
        }

        return sb.ToString();
    }

    #endregion Public Methods
}
