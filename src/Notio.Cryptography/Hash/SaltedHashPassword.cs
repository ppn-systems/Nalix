using System;
using System.Security.Cryptography;
using System.Text;

namespace Notio.Cryptography.Hash;

/// <summary>
/// Provides methods for securely protecting and unprotecting passwords.
/// </summary>
/// <remarks>
/// This class uses encryption or secure storage techniques to protect sensitive password data. It is designed
/// to ensure that passwords can be securely stored and retrieved without exposing them in plaintext.
/// </remarks>
public static class PasswordProtector
{
    /// <summary>
    /// Hashes the password with a randomly generated salt.
    /// </summary>
    /// <param name="password">The password to hash.</param>
    /// <returns>A string containing the salt and hash, separated by a colon.</returns>
    public static string Hash(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password cannot be null, empty, or whitespace.", nameof(password));

        const int SaltSize = 16; // Size of the salt in bytes, adjust as needed

        // Generate a random salt
        byte[] salt = new byte[SaltSize];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }

        // Combine salt and password bytes
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        byte[] combinedBytes = new byte[salt.Length + passwordBytes.Length];
        Buffer.BlockCopy(salt, 0, combinedBytes, 0, salt.Length);
        Buffer.BlockCopy(passwordBytes, 0, combinedBytes, salt.Length, passwordBytes.Length);

        // Compute the hash
        byte[] hashBytes;
        hashBytes = SHA256.HashData(combinedBytes);

        // Convert to hexadecimal strings for storage
        string saltHex = ByteArrayToHexString(salt);
        string hashHex = ByteArrayToHexString(hashBytes);

        return $"{saltHex}:{hashHex}";
    }

    /// <summary>
    /// Verifies the password against the stored hash.
    /// </summary>
    /// <param name="password">The password to verify.</param>
    /// <param name="storedHash">The stored hash value.</param>
    /// <returns>True if the password matches; otherwise, false.</returns>
    public static bool Verify(string password, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password cannot be null, empty, or whitespace.", nameof(password));
        if (string.IsNullOrWhiteSpace(storedHash))
            throw new ArgumentException("Stored hash cannot be null, empty, or whitespace.", nameof(storedHash));

        // Split the stored hash into salt and hash parts
        var parts = storedHash.Split(':');
        if (parts.Length != 2)
            throw new FormatException("Invalid stored hash format.");

        byte[] salt = HexStringToByteArray(parts[0]);
        byte[] storedHashBytes = HexStringToByteArray(parts[1]);

        // Combine the salt and the submitted password
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        byte[] combinedBytes = new byte[salt.Length + passwordBytes.Length];
        Buffer.BlockCopy(salt, 0, combinedBytes, 0, salt.Length);
        Buffer.BlockCopy(passwordBytes, 0, combinedBytes, salt.Length, passwordBytes.Length);

        // Compute the hash
        byte[] hashBytes;
        hashBytes = SHA256.HashData(combinedBytes);

        // Compare the hashes securely
        return FixedTimeEquals(hashBytes, storedHashBytes);
    }

    // Converts a byte array to a hexadecimal string
    private static string ByteArrayToHexString(byte[] bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (byte b in bytes)
            sb.AppendFormat("{0:x2}", b);
        return sb.ToString();
    }

    // Converts a hexadecimal string to a byte array
    private static byte[] HexStringToByteArray(string hex)
    {
        if (hex.Length % 2 != 0)
            throw new ArgumentException("Hex string must have an even length.", nameof(hex));

        byte[] bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            string currentHex = hex.Substring(i * 2, 2);
            bytes[i] = Convert.ToByte(currentHex, 16);
        }
        return bytes;
    }

    // Securely compares two byte arrays in constant time
    private static bool FixedTimeEquals(byte[] a, byte[] b)
    {
        if (a.Length != b.Length)
            return false;

        int result = 0;
        for (int i = 0; i < a.Length; i++)
        {
            result |= a[i] ^ b[i];
        }
        return result == 0;
    }
}