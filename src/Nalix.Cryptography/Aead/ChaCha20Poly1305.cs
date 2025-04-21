using Nalix.Cryptography.Mac;
using Nalix.Cryptography.Symmetric;
using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace Nalix.Cryptography.Aead;

/// <summary>
/// Provides encryption and decryption utilities using the ChaCha20 stream cipher combined with Poly1305 for message authentication.
/// ChaCha20Poly1305 is an authenticated encryption algorithm providing both confidentiality and integrity.
/// </summary>
public static class ChaCha20Poly1305
{
    #region Constants

    /// <summary>
    /// The size of the authentication tag in bytes.
    /// </summary>
    public const int TagSize = 16;

    /// <summary>
    /// The size of the encryption key in bytes.
    /// </summary>
    private const int KeySize = 32;

    /// <summary>
    /// The size of the nonce in bytes.
    /// </summary>
    private const int NonceSize = 12;

    #endregion

    #region Public Methods

    // -------------------------------
    // Public API: Encrypt and Decrypt
    // -------------------------------

    /// <summary>
    /// Encrypts and authenticates the plaintext with the given key, nonce, and optional associated data.
    /// </summary>
    /// <param name="key">A 32-byte encryption key.</param>
    /// <param name="nonce">A 12-byte unique nonce.</param>
    /// <param name="plaintext">The plaintext data to encrypt.</param>
    /// <param name="aad">Optional additional authenticated data (AAD).</param>
    /// <returns>Encrypted ciphertext followed by a 16-byte authentication tag.</returns>
    public static byte[] Encrypt(byte[] key, byte[] nonce, byte[] plaintext, byte[] aad = null)
    {
        if (key == null || key.Length != KeySize)
            throw new ArgumentException($"Key must be {KeySize} bytes", nameof(key));

        if (nonce == null || nonce.Length != NonceSize)
            throw new ArgumentException($"Nonce must be {NonceSize} bytes", nameof(nonce));

        ArgumentNullException.ThrowIfNull(plaintext);

        byte[] result = new byte[plaintext.Length + TagSize];

        // Generate Poly1305 key using first block of ChaCha20
        byte[] poly1305Key = new byte[KeySize];
        using (ChaCha20 chacha20 = new(key, nonce, 0))
            chacha20.EncryptBytes(poly1305Key, new byte[KeySize]);

        // Encrypt plaintext
        using (ChaCha20 chacha20 = new(key, nonce, 1))
            chacha20.EncryptBytes(result, plaintext, plaintext.Length);

        // Compute MAC using Poly1305
        using (Poly1305 poly1305 = new(poly1305Key))
        {
            byte[] mac = new byte[TagSize];
            byte[] authData = PrepareAuthData(aad, result.AsSpan(0, plaintext.Length));
            poly1305.ComputeTag(authData, mac);
            Buffer.BlockCopy(mac, 0, result, plaintext.Length, TagSize);
        }

        return result;
    }

    /// <summary>
    /// Encrypts and authenticates the plaintext with the given key, nonce, and optional associated data.
    /// Uses ReadOnlySpan for improved performance.
    /// </summary>
    /// <param name="key">A 32-byte encryption key.</param>
    /// <param name="nonce">A 12-byte unique nonce.</param>
    /// <param name="plaintext">The plaintext data to encrypt.</param>
    /// <param name="aad">Optional additional authenticated data (AAD).</param>
    /// <param name="ciphertext">The encrypted output data.</param>
    /// <param name="tag">The 16-byte authentication tag.</param>
    public static void Encrypt(
        ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> aad,
        out byte[] ciphertext, out byte[] tag)
    {
        if (key.Length != KeySize)
            throw new ArgumentException($"Key must be {KeySize} bytes", nameof(key));
        if (nonce.Length != NonceSize)
            throw new ArgumentException($"Nonce must be {NonceSize} bytes", nameof(nonce));

        // Generate Poly1305 key using first block of ChaCha20
        byte[] poly1305Key = new byte[KeySize];
        using (ChaCha20 chacha20 = new(key.ToArray(), nonce.ToArray(), 0))
            chacha20.EncryptBytes(poly1305Key, new byte[KeySize]);

        // Encrypt plaintext
        ciphertext = new byte[plaintext.Length];
        using (ChaCha20 chacha20 = new(key.ToArray(), nonce.ToArray(), 1))
            chacha20.EncryptBytes(ciphertext, plaintext.ToArray(), plaintext.Length);

        // Compute MAC using Poly1305
        tag = new byte[TagSize];
        using Poly1305 poly1305 = new(poly1305Key);
        byte[] authData = PrepareAuthData(aad.ToArray(), ciphertext);
        poly1305.ComputeTag(authData, tag);
    }

    /// <summary>
    /// Decrypts and verifies the ciphertext using the given key, nonce, and optional associated data.
    /// </summary>
    /// <param name="key">A 32-byte encryption key.</param>
    /// <param name="nonce">A 12-byte nonce used during encryption.</param>
    /// <param name="ciphertext">The encrypted data with an appended 16-byte authentication tag.</param>
    /// <param name="aad">Optional associated data (AAD) used during encryption.</param>
    /// <returns>The decrypted plaintext if authentication is successful.</returns>
    public static byte[] Decrypt(byte[] key, byte[] nonce, byte[] ciphertext, byte[] aad = null)
    {
        if (key == null || key.Length != KeySize)
            throw new ArgumentException($"Key must be {KeySize} bytes", nameof(key));
        if (nonce == null || nonce.Length != NonceSize)
            throw new ArgumentException($"Nonce must be {NonceSize} bytes", nameof(nonce));
        if (ciphertext == null || ciphertext.Length < TagSize)
            throw new ArgumentException("Invalid ciphertext", nameof(ciphertext));

        // Generate Poly1305 key using first block of ChaCha20
        byte[] poly1305Key = new byte[KeySize];
        using (ChaCha20 chacha20 = new(key, nonce, 0))
            chacha20.EncryptBytes(poly1305Key, new byte[KeySize]);

        // Verify MAC
        using (Poly1305 poly1305 = new(poly1305Key))
        {
            byte[] expectedMac = new byte[TagSize];
            byte[] authData = PrepareAuthData(
                aad, ciphertext.AsSpan(0, ciphertext.Length - TagSize));
            poly1305.ComputeTag(authData, expectedMac);

            byte[] receivedMac = new byte[TagSize];
            Buffer.BlockCopy(
                ciphertext, ciphertext.Length - TagSize,
                receivedMac, 0, TagSize);

            if (!CompareBytes(expectedMac, receivedMac))
                throw new InvalidOperationException("Authentication failed");
        }

        // Decrypt ciphertext
        byte[] plaintext = new byte[ciphertext.Length - TagSize];
        using (ChaCha20 chacha20 = new(key, nonce, 1))
            chacha20.DecryptBytes(
                plaintext, ciphertext.AsSpan(0, ciphertext.Length - TagSize).ToArray());

        return plaintext;
    }

    /// <summary>
    /// Decrypts and verifies the ciphertext using the given key, nonce, and optional associated data.
    /// Uses ReadOnlySpan for improved performance.
    /// </summary>
    /// <param name="key">A 32-byte encryption key.</param>
    /// <param name="nonce">A 12-byte nonce used during encryption.</param>
    /// <param name="ciphertext">The encrypted data.</param>
    /// <param name="aad">Optional associated data (AAD) used during encryption.</param>
    /// <param name="tag">The 16-byte authentication tag.</param>
    /// <param name="plaintext">The decrypted output data if authentication is successful.</param>
    /// <returns>True if authentication is successful, false otherwise.</returns>
    public static bool Decrypt(
        ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> ciphertext,
        ReadOnlySpan<byte> aad, ReadOnlySpan<byte> tag, out byte[] plaintext)
    {
        plaintext = null;

        if (key.Length != KeySize)
            throw new ArgumentException($"Key must be {KeySize} bytes", nameof(key));
        if (nonce.Length != NonceSize)
            throw new ArgumentException($"Nonce must be {NonceSize} bytes", nameof(nonce));
        if (tag.Length != TagSize)
            throw new ArgumentException($"Tag must be {TagSize} bytes", nameof(tag));

        // Generate Poly1305 key using first block of ChaCha20
        byte[] poly1305Key = new byte[KeySize];
        using (ChaCha20 chacha20 = new(key.ToArray(), nonce.ToArray(), 0))
            chacha20.EncryptBytes(poly1305Key, new byte[KeySize]);

        // Verify MAC
        using (Poly1305 poly1305 = new(poly1305Key))
        {
            byte[] expectedTag = new byte[TagSize];
            byte[] authData = PrepareAuthData(aad.ToArray(), ciphertext);
            poly1305.ComputeTag(authData, expectedTag);

            // Constant-time comparison to prevent timing attacks (tấn công dựa vào thời gian)
            if (!CompareBytes(expectedTag, tag.ToArray()))
                return false;
        }

        // Decrypt ciphertext
        plaintext = new byte[ciphertext.Length];
        using (ChaCha20 chacha20 = new(key.ToArray(), nonce.ToArray(), 1))
            chacha20.DecryptBytes(plaintext, ciphertext.ToArray());

        return true;
    }

    #endregion

    #region Private Methods

    // ----------------------------
    // Private API: Utility Methods
    // ----------------------------

    /// <summary>
    /// Securely compares two byte arrays in constant time to prevent timing attacks.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CompareBytes(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        uint diff = 0; for (int i = 0; i < a.Length; i++) diff |= (uint)(a[i] ^ b[i]); return diff == 0;
    }

    /// <summary>
    /// Prepares the authenticated data input for Poly1305.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte[] PrepareAuthData(byte[] associatedData, ReadOnlySpan<byte> ciphertext)
    {
        long adLength = associatedData?.Length ?? 0;
        long ctLength = ciphertext.Length;

        byte[] macData = new byte[
            (associatedData?.Length ?? 0) +
            (adLength % 16 == 0 ? 0 : 16 - (adLength % 16)) +
            ciphertext.Length +
            (ctLength % 16 == 0 ? 0 : 16 - (ctLength % 16)) +
            16
        ];

        int offset = 0;

        if (associatedData != null && associatedData.Length > 0)
        {
            Buffer.BlockCopy(associatedData, 0, macData, offset, associatedData.Length);
            offset += associatedData.Length;
            if (associatedData.Length % 16 != 0)
                offset += 16 - (associatedData.Length % 16);
        }

        ciphertext.CopyTo(macData.AsSpan(offset));
        offset += ciphertext.Length;
        if (ciphertext.Length % 16 != 0)
            offset += 16 - (ciphertext.Length % 16);

        BinaryPrimitives.WriteUInt64LittleEndian(macData.AsSpan(offset), (ulong)adLength);
        BinaryPrimitives.WriteUInt64LittleEndian(macData.AsSpan(offset + 8), (ulong)ctLength);

        return macData;
    }

    #endregion
}
