using Notio.Cryptography.Mac;
using Notio.Cryptography.Symmetric;
using System;
using System.Buffers.Binary;

namespace Notio.Cryptography.Aead;

/// <summary>
/// Implements the ChaCha20-Poly1305 AEAD construction as specified in RFC 8439.
/// </summary>
public sealed class ChaCha20Poly1305 : IDisposable
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;

    private readonly byte[] _key;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChaCha20Poly1305"/> class with the specified key.
    /// </summary>
    /// <param name="key">A 32-byte key for encryption and decryption.</param>
    /// <exception cref="ArgumentException">Thrown if the key is null or not 32 bytes long.</exception>
    public ChaCha20Poly1305(byte[] key)
    {
        if (key == null || key.Length != KeySize)
            throw new ArgumentException($"Key must be {KeySize} bytes", nameof(key));

        _key = (byte[])key.Clone();
    }

    /// <summary>
    /// Encrypts and authenticates the plaintext with the given nonce and optional associated data.
    /// </summary>
    /// <param name="nonce">A 12-byte unique nonce for encryption.</param>
    /// <param name="plaintext">The plaintext to be encrypted.</param>
    /// <param name="associatedData">Optional additional authenticated data (AAD).</param>
    /// <returns>A byte array containing the encrypted ciphertext followed by a 16-byte authentication tag.</returns>
    /// <exception cref="ArgumentException">Thrown if the nonce is null or not 12 bytes long.</exception>
    /// <exception cref="ArgumentNullException">Thrown if the plaintext is null.</exception>
    public byte[] Encrypt(byte[] nonce, byte[] plaintext, byte[] associatedData = null)
    {
        if (nonce == null || nonce.Length != NonceSize)
            throw new ArgumentException($"Nonce must be {NonceSize} bytes", nameof(nonce));

        ArgumentNullException.ThrowIfNull(plaintext);

        byte[] result = new byte[plaintext.Length + TagSize];

        // Generate Poly1305 key using first block of ChaCha20
        byte[] poly1305Key = new byte[KeySize];
        using (var chacha20 = new ChaCha20(_key, nonce, 0))
        {
            chacha20.EncryptBytes(poly1305Key, new byte[32]);
        }

        // Encrypt plaintext
        using (var chacha20 = new ChaCha20(_key, nonce, 1))
        {
            chacha20.EncryptBytes(result, plaintext, plaintext.Length);
        }

        // Compute MAC using Poly1305
        using (var poly1305 = new Poly1305(poly1305Key))
        {
            byte[] mac = new byte[TagSize];
            byte[] authData = PrepareAuthData(associatedData, result.AsSpan(0, plaintext.Length));
            poly1305.ComputeTag(authData, mac);
            Buffer.BlockCopy(mac, 0, result, plaintext.Length, TagSize);
        }

        return result;
    }

    /// <summary>
    /// Decrypts the given ciphertext and verifies its authentication tag.
    /// </summary>
    /// <param name="nonce">A 12-byte nonce used during encryption.</param>
    /// <param name="ciphertext">The encrypted data with an appended 16-byte authentication tag.</param>
    /// <param name="associatedData">Optional associated data (AAD) used during encryption.</param>
    /// <returns>The decrypted plaintext if authentication is successful.</returns>
    /// <exception cref="ArgumentException">Thrown if the nonce is null or not 12 bytes long.</exception>
    /// <exception cref="ArgumentException">Thrown if the ciphertext is null or too short.</exception>
    /// <exception cref="InvalidOperationException">Thrown if authentication fails.</exception>
    public byte[] Decrypt(byte[] nonce, byte[] ciphertext, byte[] associatedData = null)
    {
        if (nonce == null || nonce.Length != NonceSize)
            throw new ArgumentException($"Nonce must be {NonceSize} bytes", nameof(nonce));

        if (ciphertext == null || ciphertext.Length < TagSize)
            throw new ArgumentException("Invalid ciphertext", nameof(ciphertext));

        // Generate Poly1305 key using first block of ChaCha20
        byte[] poly1305Key = new byte[KeySize];
        using (var chacha20 = new ChaCha20(_key, nonce, 0))
        {
            chacha20.EncryptBytes(poly1305Key, new byte[32]);
        }

        // Verify MAC
        using (var poly1305 = new Poly1305(poly1305Key))
        {
            byte[] expectedMac = new byte[TagSize];
            byte[] authData = PrepareAuthData(associatedData, ciphertext.AsSpan(0, ciphertext.Length - TagSize));
            poly1305.ComputeTag(authData, expectedMac);

            byte[] receivedMac = new byte[TagSize];
            Buffer.BlockCopy(ciphertext, ciphertext.Length - TagSize, receivedMac, 0, TagSize);

            if (!CompareBytes(expectedMac, receivedMac))
                throw new InvalidOperationException("Authentication failed");
        }

        // Decrypt ciphertext
        byte[] plaintext = new byte[ciphertext.Length - TagSize];
        using (var chacha20 = new ChaCha20(_key, nonce, 1))
        {
            chacha20.DecryptBytes(plaintext, ciphertext.AsSpan(0, ciphertext.Length - TagSize).ToArray());
        }

        return plaintext;
    }

    /// <summary>
    /// Securely compares two byte arrays in constant time to prevent timing attacks.
    /// </summary>
    /// <param name="a">First byte array.</param>
    /// <param name="b">Second byte array.</param>
    /// <returns>True if the arrays are identical; otherwise, false.</returns>
    private static bool CompareBytes(byte[] a, byte[] b)
    {
        if (a.Length != b.Length)
            return false;

        uint diff = 0;
        for (int i = 0; i < a.Length; i++)
        {
            diff |= (uint)(a[i] ^ b[i]);
        }
        return diff == 0;
    }

    /// <summary>
    /// Prepares the authenticated data input for Poly1305.
    /// </summary>
    /// <param name="associatedData">Optional associated data (AAD).</param>
    /// <param name="ciphertext">Ciphertext without the authentication tag.</param>
    /// <returns>A byte array formatted for Poly1305 authentication.</returns>
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

    /// <summary>
    /// Releases resources and securely clears the encryption key from memory.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            if (_key != null)
            {
                Array.Clear(_key, 0, _key.Length);
            }
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
