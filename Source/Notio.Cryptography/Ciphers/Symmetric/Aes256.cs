using System;

namespace Notio.Cryptography.Ciphers.Symmetric;

/// <summary>
/// Provides AES-256 encryption and decryption utilities with CTR and CFB modes.
/// </summary>
public static class Aes256
{
    public const int IvSize = 16;  // 128-bit IV
    public const int BlockSize = 16;  // AES block size in bytes
    public const int BlockSizeBits = 128;

    public static class CbcMode
    {
        public static ReadOnlyMemory<byte> Encrypt(
            ReadOnlyMemory<byte> plaintext, ReadOnlyMemory<byte> key)
            => AesMode.Cbc.Encrypt(plaintext, key);

        public static ReadOnlyMemory<byte> Decrypt(
            ReadOnlyMemory<byte> ciphertext, ReadOnlyMemory<byte> key)
            => AesMode.Cbc.Decrypt(ciphertext, key);
    }

    public static class GcmMode
    {
        public static ReadOnlyMemory<byte> Encrypt(
            ReadOnlyMemory<byte> plaintext, ReadOnlyMemory<byte> key)
            => AesMode.Gcm.Encrypt(plaintext, key);

        public static ReadOnlyMemory<byte> Decrypt(
            ReadOnlyMemory<byte> ciphertext, ReadOnlyMemory<byte> key)
            => AesMode.Gcm.Decrypt(ciphertext, key);
    }

    public static class CtrMode
    {
        public static ReadOnlyMemory<byte> Encrypt(
            ReadOnlyMemory<byte> plaintext, ReadOnlyMemory<byte> key)
            => AesMode.Ctr.Encrypt(plaintext, key);

        public static ReadOnlyMemory<byte> Decrypt(
            ReadOnlyMemory<byte> ciphertext, ReadOnlyMemory<byte> key)
            => AesMode.Ctr.Decrypt(ciphertext, key);
    }
}