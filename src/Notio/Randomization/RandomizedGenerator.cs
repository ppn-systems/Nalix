using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace Notio.Randomization;

/// <summary>
/// Provides secure cryptographic key and nonce generation without using System.Security.Cryptography.
/// </summary>
public static class RandomizedGenerator
{
    private static readonly ulong[] State = new ulong[4];

    static RandomizedGenerator()
    {
        State[0] = (ulong)DateTime.UtcNow.Ticks;
        State[1] = (ulong)Environment.TickCount64;
        State[2] = (ulong)new Random().NextInt64();
        State[3] = (ulong)new Random().NextInt64();
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
            throw new ArgumentException("Key length must be greater than zero.", nameof(length));

        byte[] key = new byte[length];
        Fill(key);
        return key;
    }

    /// <summary>
    /// Generates a secure 12-byte nonce (96 bits) for encryption.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] CreateNonce()
    {
        byte[] nonce = new byte[12];
        Fill(nonce);
        return nonce;
    }

    /// <summary>
    /// Fills the provided span with cryptographically strong random bytes using Xoshiro256++.
    /// </summary>
    /// <param name="data">The span to fill with random bytes.</param>
    public static void Fill(Span<byte> data)
    {
        int i = 0;
        while (i + 8 <= data.Length)
        {
            BitConverter.TryWriteBytes(data.Slice(i, 8), Next());
            i += 8;
        }
        if (i < data.Length)
        {
            ulong last = Next();
            for (int j = 0; j < data.Length - i; j++)
                data[i + j] = (byte)(last >> (j * 8));
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
        byte[] bytes = new byte[length];
        Fill(bytes);
        return bytes;
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
            throw new ArgumentException("XTEA key must be {16} bytes.", nameof(key));

        uint[] uintKey = new uint[4];
        Buffer.BlockCopy(key, 0, uintKey, 0, 16);
        return uintKey;
    }

    /// <summary>
    /// Derives a cryptographic key from a passphrase with the specified length.
    /// </summary>
    /// <param name="passphrase">The input passphrase.</param>
    /// <param name="length">The desired key length in bytes.</param>
    /// <param name="iterations">The number of iterations for key stretching.</param>
    /// <returns>A derived key of the specified length.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] DeriveKey(string passphrase, int length = 32, int iterations = 100000)
    {
        if (string.IsNullOrEmpty(passphrase))
            throw new ArgumentException("Passphrase cannot be null or empty.", nameof(passphrase));

        if (length <= 0)
            throw new ArgumentException("Key length must be greater than zero.", nameof(length));

        if (iterations <= 0)
            throw new ArgumentException("Iterations must be greater than zero.", nameof(iterations));

        byte[] passphraseBytes = Encoding.UTF8.GetBytes(passphrase);
        byte[] key = new byte[length];

        for (int i = 0; i < length; i++)
        {
            byte hash = (byte)(i + 1);
            for (int iter = 0; iter < iterations; iter++)
            {
                foreach (byte b in passphraseBytes)
                {
                    hash ^= RotateLeft((byte)(b + iter), (i % 8) + 1);
                }
            }
            key[i] = hash;
        }

        return key;
    }

    /// <summary>
    /// Generates a cryptographically strong 64-bit random number.
    /// </summary>
    /// <returns>A 64-bit unsigned random number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Next()
    {
        ulong result = State[0] + State[3];

        ulong t = State[1] << 17;

        State[2] ^= State[0];
        State[3] ^= State[1];
        State[1] ^= State[2];
        State[0] ^= State[3];

        State[2] ^= t;

        State[3] = RotateLeft(State[3], 45);

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong RotateLeft(ulong x, int k) => x << k | x >> 64 - k;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte RotateLeft(byte x, int k) => (byte)((x << k) | (x >> (8 - k)));
}
