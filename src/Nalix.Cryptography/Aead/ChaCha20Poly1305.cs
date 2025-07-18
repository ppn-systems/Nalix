using Nalix.Cryptography.Mac;
using Nalix.Cryptography.Symmetric.Stream;

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
    public const System.Int32 TagSize = 16;

    /// <summary>
    /// The size of the encryption key in bytes.
    /// </summary>
    private const System.Int32 KeySize = 32;

    /// <summary>
    /// The size of the nonce in bytes.
    /// </summary>
    private const System.Int32 NonceSize = 12;

    #endregion Constants

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
    public static System.Byte[] Encrypt(System.Byte[] key, System.Byte[] nonce, System.Byte[] plaintext, System.Byte[] aad = null)
    {
        if (key == null || key.Length != KeySize)
        {
            throw new System.ArgumentException($"Key must be {KeySize} bytes", nameof(key));
        }

        if (nonce == null || nonce.Length != NonceSize)
        {
            throw new System.ArgumentException($"Nonce must be {NonceSize} bytes", nameof(nonce));
        }

        System.ArgumentNullException.ThrowIfNull(plaintext);

        System.Byte[] result = new System.Byte[plaintext.Length + TagSize];

        // Generate Poly1305 key using first block of ChaCha20
        System.Byte[] poly1305Key = new System.Byte[KeySize];
        using (ChaCha20 chacha20 = new(key, nonce, 0))
        {
            chacha20.EncryptBytes(poly1305Key, new System.Byte[KeySize]);
        }

        // Encrypt plaintext
        using (ChaCha20 chacha20 = new(key, nonce, 1))
        {
            chacha20.EncryptBytes(result, plaintext, plaintext.Length);
        }

        // Compute MAC using Poly1305
        using (Poly1305 poly1305 = new(poly1305Key))
        {
            System.Byte[] mac = new System.Byte[TagSize];
            System.Byte[] authData = PrepareAuthData(aad, System.MemoryExtensions.AsSpan(result, 0, plaintext.Length));
            poly1305.ComputeTag(authData, mac);
            System.Buffer.BlockCopy(mac, 0, result, plaintext.Length, TagSize);
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
        System.ReadOnlySpan<System.Byte> key, System.ReadOnlySpan<System.Byte> nonce,
        System.ReadOnlySpan<System.Byte> plaintext, System.ReadOnlySpan<System.Byte> aad,
        out System.Byte[] ciphertext, out System.Byte[] tag)
    {
        if (key.Length != KeySize)
        {
            throw new System.ArgumentException($"Key must be {KeySize} bytes", nameof(key));
        }

        if (nonce.Length != NonceSize)
        {
            throw new System.ArgumentException($"Nonce must be {NonceSize} bytes", nameof(nonce));
        }

        // Generate Poly1305 key using first block of ChaCha20
        System.Byte[] poly1305Key = new System.Byte[KeySize];
        using (ChaCha20 chacha20 = new(key.ToArray(), nonce.ToArray(), 0))
        {
            chacha20.EncryptBytes(poly1305Key, new System.Byte[KeySize]);
        }

        // Encrypt plaintext
        ciphertext = new System.Byte[plaintext.Length];
        using (ChaCha20 chacha20 = new(key.ToArray(), nonce.ToArray(), 1))
        {
            chacha20.EncryptBytes(ciphertext, plaintext.ToArray(), plaintext.Length);
        }

        // Compute MAC using Poly1305
        tag = new System.Byte[TagSize];
        using Poly1305 poly1305 = new(poly1305Key);
        System.Byte[] authData = PrepareAuthData(aad.ToArray(), ciphertext);
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
    public static System.Byte[] Decrypt(System.Byte[] key, System.Byte[] nonce, System.Byte[] ciphertext, System.Byte[] aad = null)
    {
        if (key == null || key.Length != KeySize)
        {
            throw new System.ArgumentException($"Key must be {KeySize} bytes", nameof(key));
        }

        if (nonce == null || nonce.Length != NonceSize)
        {
            throw new System.ArgumentException($"Nonce must be {NonceSize} bytes", nameof(nonce));
        }

        if (ciphertext == null || ciphertext.Length < TagSize)
        {
            throw new System.ArgumentException("Invalid ciphertext", nameof(ciphertext));
        }

        // Generate Poly1305 key using first block of ChaCha20
        System.Byte[] poly1305Key = new System.Byte[KeySize];
        using (ChaCha20 chacha20 = new(key, nonce, 0))
        {
            chacha20.EncryptBytes(poly1305Key, new System.Byte[KeySize]);
        }

        // Verify MAC
        using (Poly1305 poly1305 = new(poly1305Key))
        {
            System.Byte[] expectedMac = new System.Byte[TagSize];
            System.Byte[] authData = PrepareAuthData(
                aad, System.MemoryExtensions.AsSpan(ciphertext, 0, ciphertext.Length - TagSize));
            poly1305.ComputeTag(authData, expectedMac);

            System.Byte[] receivedMac = new System.Byte[TagSize];
            System.Buffer.BlockCopy(
                ciphertext, ciphertext.Length - TagSize,
                receivedMac, 0, TagSize);

            if (!CompareBytes(expectedMac, receivedMac))
            {
                throw new System.InvalidOperationException("Authentication failed");
            }
        }

        // Decrypt ciphertext
        System.Byte[] plaintext = new System.Byte[ciphertext.Length - TagSize];
        using (ChaCha20 chacha20 = new(key, nonce, 1))
        {
            chacha20.DecryptBytes(
                plaintext, System.MemoryExtensions.AsSpan(ciphertext, 0, ciphertext.Length - TagSize).ToArray());
        }

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
    public static System.Boolean Decrypt(
        System.ReadOnlySpan<System.Byte> key, System.ReadOnlySpan<System.Byte> nonce, System.ReadOnlySpan<System.Byte> ciphertext,
        System.ReadOnlySpan<System.Byte> aad, System.ReadOnlySpan<System.Byte> tag, out System.Byte[] plaintext)
    {
        plaintext = null;

        if (key.Length != KeySize)
        {
            throw new System.ArgumentException($"Key must be {KeySize} bytes", nameof(key));
        }

        if (nonce.Length != NonceSize)
        {
            throw new System.ArgumentException($"Nonce must be {NonceSize} bytes", nameof(nonce));
        }

        if (tag.Length != TagSize)
        {
            throw new System.ArgumentException($"Tag must be {TagSize} bytes", nameof(tag));
        }

        // Generate Poly1305 key using first block of ChaCha20
        System.Byte[] poly1305Key = new System.Byte[KeySize];
        using (ChaCha20 chacha20 = new(key.ToArray(), nonce.ToArray(), 0))
        {
            chacha20.EncryptBytes(poly1305Key, new System.Byte[KeySize]);
        }

        // Verify MAC
        using (Poly1305 poly1305 = new(poly1305Key))
        {
            System.Byte[] expectedTag = new System.Byte[TagSize];
            System.Byte[] authData = PrepareAuthData(aad.ToArray(), ciphertext);
            poly1305.ComputeTag(authData, expectedTag);

            // Constant-time comparison to prevent timing attacks (tấn công dựa vào thời gian)
            if (!CompareBytes(expectedTag, tag.ToArray()))
            {
                return false;
            }
        }

        // Decrypt ciphertext
        plaintext = new System.Byte[ciphertext.Length];
        using (ChaCha20 chacha20 = new(key.ToArray(), nonce.ToArray(), 1))
        {
            chacha20.DecryptBytes(plaintext, ciphertext.ToArray());
        }

        return true;
    }

    #endregion Public Methods

    #region Private Methods

    // ----------------------------
    // Private API: Utility Methods
    // ----------------------------

    /// <summary>
    /// Securely compares two byte arrays in constant time to prevent timing attacks.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Boolean CompareBytes(System.Byte[] a, System.Byte[] b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        System.UInt32 diff = 0; for (System.Int32 i = 0; i < a.Length; i++)
        {
            diff |= (System.UInt32)(a[i] ^ b[i]);
        }

        return diff == 0;
    }

    /// <summary>
    /// Prepares the authenticated data input for Poly1305.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Byte[] PrepareAuthData(
        System.Byte[] associatedData,
        System.ReadOnlySpan<System.Byte> ciphertext)
    {
        System.Int64 adLength = associatedData?.Length ?? 0;
        System.Int64 ctLength = ciphertext.Length;

        System.Byte[] macData = new System.Byte[
            (associatedData?.Length ?? 0) +
            (adLength % 16 == 0 ? 0 : 16 - (adLength % 16)) +
            ciphertext.Length +
            (ctLength % 16 == 0 ? 0 : 16 - (ctLength % 16)) +
            16
        ];

        System.Int32 offset = 0;

        if (associatedData != null && associatedData.Length > 0)
        {
            System.Buffer.BlockCopy(associatedData, 0, macData, offset, associatedData.Length);
            offset += associatedData.Length;
            if (associatedData.Length % 16 != 0)
            {
                offset += 16 - (associatedData.Length % 16);
            }
        }

        ciphertext.CopyTo(System.MemoryExtensions.AsSpan(macData, offset));
        offset += ciphertext.Length;
        if (ciphertext.Length % 16 != 0)
        {
            offset += 16 - (ciphertext.Length % 16);
        }

        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(
            System.MemoryExtensions.AsSpan(macData, offset), (System.UInt64)adLength);

        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(
            System.MemoryExtensions.AsSpan(macData, offset + 8), (System.UInt64)ctLength);

        return macData;
    }

    #endregion Private Methods
}
