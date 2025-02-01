using Notio.Common;
using System;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

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

    /// <summary>
    /// Derives a 256-bit AES key from a string.
    /// </summary>
    /// <param name="input">The input string.</param>
    /// <returns>A 256-bit key.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] GenerateKey(string input)
    {
        if (string.IsNullOrEmpty(input))
            throw new ArgumentException("Input cannot be null or empty.", nameof(input));

        return SHA256.HashData(Encoding.UTF8.GetBytes(input));
    }

    /// <summary>
    /// Generates a secure random 96-bit nonce.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] GenerateNonce()
    {
        byte[] nonce = new byte[12];
        RandomNumberGenerator.Fill(nonce);
        return nonce;
    }

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