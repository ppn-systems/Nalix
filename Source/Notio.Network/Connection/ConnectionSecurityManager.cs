using Notio.Cryptography.Ciphers.Asymmetric;
using System;
using System.Security.Cryptography;
using ChaCha20Poly1305 = Notio.Cryptography.Ciphers.Symmetric.ChaCha20Poly1305;

namespace Notio.Network.Connection;

/// <summary>
/// Manages the connection security, including key exchange and data encryption/decryption.
/// </summary>
public class ConnectionSecurityManager
{
    private byte[] _encryptionKey;
    private readonly byte[] _x25519PublicKey;
    private readonly byte[] _x25519PrivateKey;

    /// <summary>
    /// Gets the symmetric encryption key.
    /// </summary>
    public byte[] EncryptionKey => _encryptionKey;

    /// <summary>
    /// Gets the asymmetric public key.
    /// </summary>
    public byte[] PublicKey => _x25519PublicKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionSecurityManager"/> class.
    /// </summary>
    public ConnectionSecurityManager()
    {
        _encryptionKey = [];
        (_x25519PrivateKey, _x25519PublicKey) = X25519.GenerateKeyPair();
    }

    /// <summary>
    /// Receives a public key from client, computes the shared secret, and sets the encryption key.
    /// </summary>
    /// <param name="clientPublicKey">Client's public key.</param>
    public void ComputeAndEncryptSharedSecret(byte[] clientPublicKey)
    {
        // Compute shared secret using client's public key and server's private key
        byte[] sharedSecret = X25519.ComputeSharedSecret(_x25519PrivateKey, clientPublicKey);

        // Derive encryption key from shared secret (e.g., using SHA-256)
        _encryptionKey = SHA256.HashData(sharedSecret);

        if (_encryptionKey == null || _encryptionKey.Length == 0)
        {
            throw new InvalidOperationException("Failed to derive encryption key.");
        }
    }

    /// <summary>
    /// Encrypts the provided data using ChaCha20-Poly1305.
    /// The output format is: nonce (12 bytes) || ciphertext || tag (16 bytes)
    /// </summary>
    /// <param name="data">The data to encrypt.</param>
    /// <returns>The encrypted data.</returns>
    public ReadOnlySpan<byte> Encrypt(ReadOnlyMemory<byte> data)
    {
        if (_encryptionKey == null)
            throw new InvalidOperationException("Encryption key has not been set. Please call ComputeAndEncryptSharedSecret first.");

        byte[] nonce = new byte[12];
        RandomNumberGenerator.Fill(nonce);

        // Encrypt using ChaCha20-Poly1305.
        ChaCha20Poly1305.Encrypt(_encryptionKey, nonce, data.Span, null, out byte[] ciphertext, out byte[] tag);

        // Combine nonce, ciphertext, and tag for transmission
        byte[] result = new byte[12 + ciphertext.Length + 16];
        Buffer.BlockCopy(nonce, 0, result, 0, 12);
        Buffer.BlockCopy(ciphertext, 0, result, 12, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, result, 12 + ciphertext.Length, 16);

        return result;
    }

    /// <summary>
    /// Decrypts the provided data using ChaCha20-Poly1305.
    /// The input data should be in the format: nonce (12 bytes) || ciphertext || tag (16 bytes).
    /// </summary>
    /// <param name="data">The data to decrypt.</param>
    /// <returns>The decrypted data.</returns>
    public ReadOnlySpan<byte> Decrypt(ReadOnlyMemory<byte> data)
    {
        if (_encryptionKey == null)
            throw new InvalidOperationException("Encryption key has not been set. Please call ComputeAndEncryptSharedSecret first.");

        ReadOnlySpan<byte> input = data.Span;
        if (input.Length < 12 + 16)
            throw new ArgumentException("Invalid data length.");

        ReadOnlySpan<byte> nonce = input[..12];
        ReadOnlySpan<byte> tag = input.Slice(input.Length - 16, 16);
        ReadOnlySpan<byte> ciphertext = input.Slice(12, input.Length - 12 - 16);

        bool success = ChaCha20Poly1305.Decrypt(_encryptionKey, nonce, ciphertext, null, tag, out byte[] plaintext);
        if (!success)
            throw new CryptographicException("Authentication failed.");

        return plaintext;
    }
}