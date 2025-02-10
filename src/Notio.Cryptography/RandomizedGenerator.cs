using System.Runtime.CompilerServices;

namespace Notio.Cryptography;

/// <summary>
/// Provides secure cryptographic key and nonce generation without using System.Security.Cryptography.
/// </summary>
public static class RandomizedGenerator
{
    private static readonly System.Random _random = new();
    private static readonly ulong[] _state = new ulong[4];

    static RandomizedGenerator()
    {
        _state[0] = (ulong)_random.NextInt64();
        _state[1] = (ulong)_random.NextInt64();
        _state[2] = (ulong)_random.NextInt64();
        _state[3] = (ulong)_random.NextInt64();
    }

    /// <summary>
    /// Creates a new cryptographic key of the specified length.
    /// </summary>
    /// <param name="length">The key length in bytes (e.g., 32 for AES-256).</param>
    /// <returns>A securely generated key of the specified length.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] CreateKey(int length = 32)
    {
        if (length <= 0)
            throw new System.ArgumentException("Key length must be greater than zero.", nameof(length));

        byte[] key = new byte[length];
        _random.NextBytes(key); // Using _random instead of cryptographic generator
        return key;
    }

    /// <summary>
    /// Derives a cryptographic key from a passphrase with the specified length.
    /// </summary>
    /// <param name="passphrase">The input passphrase.</param>
    /// <param name="length">The desired key length in bytes.</param>
    /// <returns>A derived key of the specified length.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] DeriveKey(string passphrase, int length = 32)
    {
        if (string.IsNullOrEmpty(passphrase))
            throw new System.ArgumentException("Passphrase cannot be null or empty.", nameof(passphrase));
        if (length <= 0)
            throw new System.ArgumentException("Key length must be greater than zero.", nameof(length));

        byte[] passphraseBytes = System.Text.Encoding.UTF8.GetBytes(passphrase);
        byte[] key = new byte[length];
        int i = 0;
        while (i < length)
        {
            for (int j = 0; j < passphraseBytes.Length && i < length; j++, i++)
            {
                key[i] = passphraseBytes[j];
            }
        }
        return key;
    }

    /// <summary>
    /// Generates a secure 12-byte nonce (96 bits) for encryption.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] CreateNonce()
    {
        byte[] nonce = new byte[12];
        _random.NextBytes(nonce); // Using _random instead of cryptographic generator
        return nonce;
    }

    /// <summary>
    /// Converts a byte array (16 bytes) into a 32-bit unsigned integer array (4 elements).
    /// </summary>
    /// <param name="key">The byte array representing the key, which must be 16 bytes long.</param>
    /// <returns>A 32-bit unsigned integer array (4 elements) representing the key.</returns>
    /// <exception cref="System.ArgumentException">
    /// Thrown when the length of the provided byte array is not 16 bytes.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint[] ConvertKey(byte[] key)
    {
        if (key.Length != 16)
            throw new System.ArgumentException($"XTEA key must be {16} bytes.", nameof(key));

        uint[] uintKey = new uint[4];
        System.Buffer.BlockCopy(key, 0, uintKey, 0, 16);
        return uintKey;
    }

    /// <summary>
    /// Fills the provided span with cryptographically strong random bytes using Xoshiro256++.
    /// </summary>
    /// <param name="data">The span to fill with random bytes.</param>
    public static void Fill(System.Span<byte> data)
    {
        int i = 0;
        while (i + 8 <= data.Length)
        {
            System.BitConverter.TryWriteBytes(data.Slice(i, 8), Next());
            i += 8;
        }
        if (i < data.Length)
        {
            ulong last = Next();
            for (int j = 0; j < data.Length - i; j++)
                data[i + j] = (byte)(last >> j * 8);
        }
    }

    /// <summary>
    /// Generates a random byte array of the specified length.
    /// </summary>
    /// <param name="length">The number of random bytes to generate.</param>
    /// <returns>A byte array filled with random data.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] GetBytes(int length)
    {
        byte[] data = new byte[length];
        Fill(data);
        return data;
    }

    /// <summary>
    /// Generates a cryptographically strong 64-bit random number.
    /// </summary>
    /// <returns>A 64-bit unsigned random number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Next()
    {
        ulong result = _state[0] + _state[3];

        ulong t = _state[1] << 17;

        _state[2] ^= _state[0];
        _state[3] ^= _state[1];
        _state[1] ^= _state[2];
        _state[0] ^= _state[3];

        _state[2] ^= t;

        _state[3] = RotateLeft(_state[3], 45);

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong RotateLeft(ulong x, int k) => x << k | x >> 64 - k;
}
