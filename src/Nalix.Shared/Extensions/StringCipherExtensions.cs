// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Enums;
using Nalix.Shared.Security;

namespace Nalix.Shared.Extensions;

/// <summary>
/// Provides convenience methods to encrypt/decrypt UTF-8 text with Base64 IEndpointKey /O on top of <see cref="EnvelopeCipher"/>.
/// </summary>
public static class StringCipherExtensions
{
    /// <summary>
    /// Encrypts the specified text using UTF-8 encoding and returns a Base64 string of the ciphertext.
    /// </summary>
    /// <param name="this">The UTF-8 text to encrypt. If null or empty, returns <see cref="System.String.Empty"/>.</param>
    /// <param name="key">The encryption key.</param>
    /// <param name="algorithm">The symmetric algorithm to use.</param>
    /// <returns>A Base64 string of the encrypted data, or <see cref="System.String.Empty"/> if <paramref name="this"/> is null or empty.</returns>
    public static System.String EncryptToBase64(this System.String @this, System.Byte[] key, CipherSuiteType algorithm)
    {
        if (System.String.IsNullOrEmpty(@this))
        {
            return System.String.Empty;
        }

        System.Byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(@this);
        System.ReadOnlyMemory<System.Byte> cipher = EnvelopeCipher.Encrypt(key, utf8, algorithm);
        return System.Convert.ToBase64String(cipher.Span);
    }

    /// <summary>
    /// Decrypts a Base64 string produced by <see cref="EncryptToBase64"/> and returns the original UTF-8 text.
    /// </summary>
    /// <param name="this">The Base64-encoded ciphertext. If null or empty, returns <see cref="System.String.Empty"/>.</param>
    /// <param name="key">The decryption key.</param>
    /// <returns>The decrypted UTF-8 string, or <see cref="System.String.Empty"/> if <paramref name="this"/> is null or empty.</returns>
    /// <exception cref="System.InvalidOperationException">Thrown when Base64 is invalid or decryption fails.</exception>
    public static System.String DecryptFromBase64(this System.String @this, System.Byte[] key)
    {
        if (System.String.IsNullOrEmpty(@this))
        {
            return System.String.Empty;
        }

        System.Byte[] envelope;
        try
        {
            envelope = System.Convert.FromBase64String(@this);
        }
        catch (System.FormatException ex)
        {
            throw new System.InvalidOperationException("Invalid Base64 input.", ex);
        }

        if (EnvelopeCipher.Decrypt(key, envelope, out System.Byte[]? plaintext))
        {
            return System.Text.Encoding.UTF8.GetString(plaintext);
        }
        else
        {
            throw new System.InvalidOperationException("Decryption failed.");
        }
    }
}
