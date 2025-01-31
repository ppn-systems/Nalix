using Notio.Common;
using Notio.Cryptography.Ciphers.Symmetric.Mode;
using System;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace Notio.Cryptography.Ciphers.Symmetric;

/// <summary>
/// Provides AES-256 encryption and decryption utilities with CTR and CFB modes.
/// </summary>
public static class Aes256
{
    public const int IvSize = 16;  // 128-bit IV
    public const int KeySize = 32;    // AES-256 key size in bytes
    public const int BlockSize = 16;  // AES block size in bytes
    public const int BlockSizeBits = 128;

    /// <summary>
    /// Generates a new AES-256 encryption key.
    /// </summary>
    /// <returns>A 256-bit key.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] GenerateKey()
    {
        try
        {
            using var aes = Aes.Create();
            aes.KeySize = KeySize * 8; // Convert bytes to bits
            aes.GenerateKey();
            return aes.Key;
        }
        catch (Exception ex)
        {
            throw new InternalErrorException("Failed to generate encryption key", ex);
        }
    }

    public static class CbcMode
    {
        public static ReadOnlyMemory<byte> Encrypt(ReadOnlyMemory<byte> plaintext, ReadOnlyMemory<byte> key)
            => AesCbcMode.Encrypt(plaintext, key);

        public static ReadOnlyMemory<byte> Decrypt(ReadOnlyMemory<byte> ciphertext, ReadOnlyMemory<byte> key)
            => AesCbcMode.Decrypt(ciphertext, key);
    }

    public static class GcmMode
    {
        public static ReadOnlyMemory<byte> Encrypt(ReadOnlyMemory<byte> plaintext, ReadOnlyMemory<byte> key)
            => AesGcmMode.Encrypt(plaintext, key);

        public static ReadOnlyMemory<byte> Decrypt(ReadOnlyMemory<byte> ciphertext, ReadOnlyMemory<byte> key)
            => AesGcmMode.Decrypt(ciphertext, key);
    }

    public static class CtrMode
    {
        public static ReadOnlyMemory<byte> Encrypt(ReadOnlyMemory<byte> plaintext, ReadOnlyMemory<byte> key)
            => AesCtrMode.Encrypt(plaintext, key);

        public static ReadOnlyMemory<byte> Decrypt(ReadOnlyMemory<byte> ciphertext, ReadOnlyMemory<byte> key)
            => AesCtrMode.Decrypt(ciphertext, key);
    }
}