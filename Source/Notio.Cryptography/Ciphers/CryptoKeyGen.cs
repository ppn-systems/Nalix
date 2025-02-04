using System;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace Notio.Cryptography.Ciphers;

/// <summary>
/// Provides secure cryptographic key and nonce generation.
/// </summary>
public static class CryptoKeyGen
{
    /// <summary>
    /// Creates a new cryptographic key of the specified length.
    /// </summary>
    /// <param name="length">The key length in bytes (e.g., 32 for AES-256).</param>
    /// <returns>A securely generated key of the specified length.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] CreateKey(int length = 32)
    {
        if (length <= 0)
            throw new ArgumentException("Key length must be greater than zero.", nameof(length));

        byte[] key = new byte[length];
        RandomNumberGenerator.Fill(key);
        return key;
    }

    /// <summary>
    /// Derives a cryptographic key from a passphrase with the specified length.
    /// </summary>
    /// <param name="passphrase">The input passphrase.</param>
    /// <param name="length">The desired key length in bytes.</param>
    /// <returns>A derived key of the specified length.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] DeriveKey(string passphrase, int length = 32)
    {
        if (string.IsNullOrEmpty(passphrase))
            throw new ArgumentException("Passphrase cannot be null or empty.", nameof(passphrase));
        if (length <= 0)
            throw new ArgumentException("Key length must be greater than zero.", nameof(length));

        byte[] hash = SHA512.HashData(Encoding.UTF8.GetBytes(passphrase)); // Use SHA-512 for better entropy
        return hash.AsSpan(0, Math.Min(length, hash.Length)).ToArray();
    }

    /// <summary>
    /// Generates a secure 12-byte nonce (96 bits) for encryption.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] CreateNonce()
    {
        byte[] nonce = new byte[12];
        RandomNumberGenerator.Fill(nonce);
        return nonce;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint[] ConvertKey(this byte[] key)
    {
        if (key.Length != 16)
            throw new ArgumentException($"XTEA key must be {16} bytes.", nameof(key));

        uint[] uintKey = new uint[4];
        Buffer.BlockCopy(key, 0, uintKey, 0, 16);
        return uintKey;
    }
}