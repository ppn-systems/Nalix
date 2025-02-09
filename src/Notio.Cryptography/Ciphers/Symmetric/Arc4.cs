using System;
using System.Runtime.CompilerServices;

namespace Notio.Cryptography.Ciphers.Symmetric;

/// <summary>
/// Implements the ARC4 (Alleged RC4) symmetric cipher.
/// </summary>
public class Arc4
{
    private byte i;
    private byte j;
    private readonly byte[] s;

    /// <summary>
    /// Initializes a new instance of the <see cref="Arc4"/> class with the given key.
    /// </summary>
    /// <param name="key">The encryption/decryption key (should be between 5 and 256 bytes).</param>
    /// <exception cref="ArgumentException">Thrown if the key is null or too short.</exception>
    public Arc4(byte[] key)
    {
        if (key == null || key.Length < 5 || key.Length > 256)
            throw new ArgumentException("Key length must be between 5 and 256 bytes.", nameof(key));

        s = new byte[256];

        for (int k = 0; k < 256; k++)
            s[k] = (byte)k;

        byte index2 = 0;
        for (int k = 0; k < 256; k++)
        {
            index2 = (byte)((index2 + key[k % key.Length] + s[k]) & 0xFF);
            Swap(s, k, index2);
        }

        i = 0;
        j = 0;
    }

    /// <summary>
    /// Encrypts or decrypts the given data in-place.
    /// </summary>
    /// <param name="buffer">The data to be processed.</param>
    public void Process(Span<byte> buffer)
    {
        for (int k = 0; k < buffer.Length; k++)
        {
            i = (byte)((i + 1) & 0xFF);
            j = (byte)((j + s[i]) & 0xFF);
            Swap(s, i, j);
            buffer[k] ^= s[(s[i] + s[j]) & 0xFF];
        }
    }

    /// <summary>
    /// Swaps two values in the state array.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Swap(byte[] s, int i, int j)
    {
        (s[i], s[j]) = (s[j], s[i]);
    }
}