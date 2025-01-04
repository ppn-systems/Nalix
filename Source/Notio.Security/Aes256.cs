using Notio.Security.Exceptions;
using System;
using System.Buffers;
using System.Security.Cryptography;

namespace Notio.Security;

internal static class Aes256
{
    internal const int BlockSize = 16;  // AES block size in bytes
    internal const int KeySize = 32;    // AES-256 key size in bytes
    internal static readonly ArrayPool<byte> Pool = ArrayPool<byte>.Shared;

    internal static void ValidateKey(byte[] key)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key), "Encryption key cannot be null");
        if (key.Length != KeySize)
            throw new ArgumentException($"Key must be {KeySize} bytes for AES-256", nameof(key));
    }

    internal static void ValidateInput(byte[] data, string paramName)
    {
        if (data == null)
            throw new ArgumentNullException(paramName, "Input data cannot be null");
        if (data.Length == 0)
            throw new ArgumentException("Input data cannot be empty", paramName);
    }

    internal static byte[] GenerateSecureIV()
    {
        var iv = new byte[BlockSize];
        try
        {
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(iv);
            return iv;
        }
        catch (Exception ex)
        {
            throw new CryptoOperationException("Failed to generate secure IV", ex);
        }
    }

    internal static void IncrementCounter(Span<byte> counter)
    {
        for (int i = counter.Length - 1; i >= 0; i--)
        {
            if (++counter[i] != 0) break;
        }
    }
}
