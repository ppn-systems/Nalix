using System;

namespace Notio.Cryptography.Ciphers.Symmetric;

/// <summary>
/// Provides AES-256 encryption and decryption utilities with CTR, CBC, and GCM modes.
/// </summary>
public static class Aes256
{
    /// <summary>
    /// The size of the Initialization Vector (IV) used in AES encryption (128 bits).
    /// </summary>
    public const int IvSize = 16;  // 128-bit IV

    /// <summary>
    /// The size of a block in AES encryption (128 bits).
    /// </summary>
    public const int BlockSize = 16;  // AES block size in bytes

    /// <summary>
    /// The block size in bits used by AES (128 bits).
    /// </summary>
    public const int BlockSizeBits = 128;

    /// <summary>
    /// AES encryption and decryption in CBC mode.
    /// </summary>
    public static class CbcMode
    {
        /// <summary>
        /// Encrypts the given plaintext using AES in CBC mode.
        /// </summary>
        /// <param name="plaintext">The plaintext to encrypt.</param>
        /// <param name="key">The AES key (256 bits) for encryption.</param>
        /// <returns>The encrypted ciphertext.</returns>
        public static Memory<byte> Encrypt(Memory<byte> plaintext, ReadOnlyMemory<byte> key)
            => AesMode.Cbc.Encrypt(plaintext, key);

        /// <summary>
        /// Decrypts the given ciphertext using AES in CBC mode.
        /// </summary>
        /// <param name="ciphertext">The ciphertext to decrypt.</param>
        /// <param name="key">The AES key (256 bits) for decryption.</param>
        /// <returns>The decrypted plaintext.</returns>
        public static Memory<byte> Decrypt(Memory<byte> ciphertext, ReadOnlyMemory<byte> key)
            => AesMode.Cbc.Decrypt(ciphertext, key);
    }

    /// <summary>
    /// AES encryption and decryption in GCM mode, which provides authenticated encryption.
    /// </summary>
    public static class GcmMode
    {
        /// <summary>
        /// Encrypts the given plaintext using AES in GCM mode.
        /// </summary>
        /// <param name="plaintext">The plaintext to encrypt.</param>
        /// <param name="key">The AES key (256 bits) for encryption.</param>
        /// <returns>The encrypted ciphertext along with the authentication tag.</returns>
        public static Memory<byte> Encrypt(Memory<byte> plaintext, ReadOnlyMemory<byte> key)
            => AesMode.Gcm.Encrypt(plaintext, key);

        /// <summary>
        /// Decrypts the given ciphertext using AES in GCM mode.
        /// </summary>
        /// <param name="ciphertext">The ciphertext to decrypt.</param>
        /// <param name="key">The AES key (256 bits) for decryption.</param>
        /// <returns>The decrypted plaintext.</returns>
        public static Memory<byte> Decrypt(Memory<byte> ciphertext, ReadOnlyMemory<byte> key)
            => AesMode.Gcm.Decrypt(ciphertext, key);
    }

    /// <summary>
    /// AES encryption and decryption in CTR mode (Counter mode).
    /// </summary>
    public static class CtrMode
    {
        /// <summary>
        /// Encrypts the given plaintext using AES in CTR mode.
        /// </summary>
        /// <param name="plaintext">The plaintext to encrypt.</param>
        /// <param name="key">The AES key (256 bits) for encryption.</param>
        /// <returns>The encrypted ciphertext.</returns>
        public static Memory<byte> Encrypt(Memory<byte> plaintext, ReadOnlyMemory<byte> key)
            => AesMode.Ctr.Encrypt(plaintext, key);

        /// <summary>
        /// Decrypts the given ciphertext using AES in CTR mode.
        /// </summary>
        /// <param name="ciphertext">The ciphertext to decrypt.</param>
        /// <param name="key">The AES key (256 bits) for decryption.</param>
        /// <returns>The decrypted plaintext.</returns>
        public static Memory<byte> Decrypt(Memory<byte> ciphertext, ReadOnlyMemory<byte> key)
            => AesMode.Ctr.Decrypt(ciphertext, key);
    }
}