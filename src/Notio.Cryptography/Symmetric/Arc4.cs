using System;
using System.Runtime.CompilerServices;

namespace Notio.Cryptography.Symmetric;

/// <summary>
/// Implements the ARC4 (Alleged RC4) symmetric stream cipher.
/// ARC4 is a stream cipher that operates on a key to generate a pseudo-random keystream,
/// which is then XORed with the plaintext or ciphertext to encrypt/decrypt data.
/// </summary>
public class Arc4
{
    private byte _i;
    private byte _j;
    private readonly byte[] _s;

    /// <summary>
    /// Initializes a new instance of the <see cref="Arc4"/> class with the given key.
    /// </summary>
    /// <param name="key">The encryption/decryption key (should be between 5 and 256 bytes).</param>
    /// <exception cref="ArgumentException">
    /// Thrown if the key is <c>null</c>, shorter than 5 bytes, or longer than 256 bytes.
    /// </exception>
    public Arc4(byte[] key)
    {
        if (key == null || key.Length < 5 || key.Length > 256)
            throw new ArgumentException("Key length must be between 5 and 256 bytes.", nameof(key));

        _s = new byte[256];

        // Initialize the permutation array
        for (int k = 0; k < 256; k++)
            _s[k] = (byte)k;

        // Key scheduling algorithm (KSA)
        byte index2 = 0;
        for (int k = 0; k < 256; k++)
        {
            index2 += (byte)(key[k % key.Length] + _s[k]);  // No need for & 0xFF as byte wraps automatically
            Swap(_s, k, index2);
        }

        _i = 0;
        _j = 0;
    }

    /// <summary>
    /// Encrypts or decrypts the given data in-place using the ARC4 stream cipher.
    /// </summary>
    /// <param name="buffer">The data buffer to be encrypted or decrypted.</param>
    public void Process(Span<byte> buffer)
    {
        for (int k = 0; k < buffer.Length; k++)
        {
            _i++;  // Implicitly wraps at 255 due to byte type
            _j += _s[_i];

            Swap(_s, _i, _j);

            // XOR with generated keystream
            buffer[k] ^= _s[_s[_i] + _s[_j] & 0xFF];
        }
    }

    /// <summary>
    /// Swaps two values in the state array.
    /// </summary>
    /// <param name="s">The state array.</param>
    /// <param name="i">The first index.</param>
    /// <param name="j">The second index.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Swap(byte[] s, int i, int j) => (s[i], s[j]) = (s[j], s[i]);
}
