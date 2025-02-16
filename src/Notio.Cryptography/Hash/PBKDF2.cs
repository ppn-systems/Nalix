using System;
using System.Text;

namespace Notio.Cryptography.Hash;

/// <summary>
/// Represents the PBKDF2 (Password-Based Key Derivation Function 2) algorithm implementation.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="PBKDF2"/> class.
/// </remarks>
public class PBKDF2
{
    private readonly byte[] _salt;
    private readonly int _keyLength;
    private readonly int _iterations;

    /// <summary>
    /// Initializes a new instance of the <see cref="PBKDF2"/> class.
    /// </summary>
    /// <param name="salt">The salt to use for the key derivation.</param>
    /// <param name="iterations">The number of iterations for the key derivation.</param>
    /// <param name="keyLength">The length of the derived key in bytes.</param>
    public PBKDF2(byte[] salt, int iterations, int keyLength)
    {
        if (salt == null || salt.Length == 0)
            throw new ArgumentException("Salt cannot be null or empty", nameof(salt));

        if (iterations <= 0)
            throw new ArgumentOutOfRangeException(nameof(iterations), "Iterations must be greater than zero.");

        if (keyLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(keyLength), "Key length must be greater than zero.");

        _salt = salt;
        _keyLength = keyLength;
        _iterations = iterations;
    }

    /// <summary>
    /// Derives a key from the given password.
    /// </summary>
    /// <param name="password">The password to derive the key from.</param>
    /// <returns>The derived key as a byte array.</returns>
    public byte[] DeriveKey(string password)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password cannot be null or empty.", nameof(password));

        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        return DeriveKeyUsingHmacSha1(passwordBytes, _salt, _iterations, _keyLength);
    }

    private static byte[] DeriveKeyUsingHmacSha1(byte[] password, byte[] salt, int iterations, int keyLength)
    {
        int hashLength = 20; // SHA-1 output size in bytes
        int blockCount = (int)Math.Ceiling((double)keyLength / hashLength);
        byte[] output = new byte[keyLength];

        for (int i = 1; i <= blockCount; i++)
        {
            byte[] block = ComputeBlock(password, salt, iterations, i);
            Array.Copy(block, 0, output, (i - 1) * hashLength,
                Math.Min(hashLength, keyLength - (i - 1) * hashLength));
        }

        return output;
    }

    private static byte[] ComputeBlock(byte[] password, byte[] salt, int iterations, int blockIndex)
    {
        // Convert block index to big-endian 4-byte array
        byte[] intBlockIndex = BitConverter.GetBytes(blockIndex);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(intBlockIndex);

        byte[] U = ComputeHmacSha1(password, CombineArrays(salt, intBlockIndex));
        byte[] result = (byte[])U.Clone();

        for (int i = 1; i < iterations; i++)
        {
            U = ComputeHmacSha1(password, U);
            for (int j = 0; j < result.Length; j++)
            {
                result[j] ^= U[j];
            }
        }
        return result;
    }

    private static byte[] ComputeHmacSha1(byte[] key, byte[] message)
    {
        int blockSize = 64;
        byte[] ipad = new byte[blockSize];
        byte[] opad = new byte[blockSize];

        // If key is longer than block size, shorten it by hashing
        if (key.Length > blockSize)
            key = SHA1.ComputeHash(key);

        // Pad key to block size if needed
        key = key.Length < blockSize ? PadKeyToBlockSize(key, blockSize) : key;

        for (int i = 0; i < blockSize; i++)
        {
            ipad[i] = (byte)(key[i] ^ 0x36);
            opad[i] = (byte)(key[i] ^ 0x5C);
        }

        byte[] innerHash = SHA1.ComputeHash(CombineArrays(ipad, message));
        return SHA1.ComputeHash(CombineArrays(opad, innerHash));
    }

    private static byte[] PadKeyToBlockSize(byte[] key, int length)
    {
        byte[] padded = new byte[length];
        Array.Copy(key, padded, key.Length);
        return padded;
    }

    private static byte[] CombineArrays(byte[] first, byte[] second)
    {
        byte[] combined = new byte[first.Length + second.Length];
        Array.Copy(first, combined, first.Length);
        Array.Copy(second, 0, combined, first.Length, second.Length);
        return combined;
    }
}
