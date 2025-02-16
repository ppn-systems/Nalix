using System;
using System.Runtime.InteropServices;

namespace Notio.Cryptography.Symmetric;

/// <summary>
/// Provides static methods for encrypting and decrypting data using the XTEA algorithm.
/// </summary>
public static class Xtea
{
    private const int NumRounds = 32;
    private const uint Delta = 0x9E3779B9;

    /// <summary>
    /// Encrypts the specified data using the XTEA algorithm.
    /// </summary>
    /// <param name="data">The data to encrypt.</param>
    /// <param name="key">The encryption key (must be exactly 4 elements).</param>
    /// <param name="output">The buffer to store the encrypted data (must be large enough to hold the result).</param>
    /// <exception cref="ArgumentException">Thrown when the data is empty or the key is not exactly 4 elements, or the output buffer is too small.</exception>
    public static void Encrypt(Memory<byte> data, ReadOnlyMemory<uint> key, Memory<byte> output)
    {
        if (data.IsEmpty)
            throw new ArgumentException("Data cannot be empty", nameof(data));
        if (key.Length != 4)
            throw new ArgumentException("Key must be exactly 4 elements", nameof(key));

        int length = data.Length;
        int paddedLength = length + 7 & ~7;

        if (output.Length < paddedLength)
            throw new ArgumentException("Output buffer is too small", nameof(output));

        data.CopyTo(output);

        if (paddedLength > length) output[length..paddedLength].Span.Clear();

        Span<uint> words = MemoryMarshal.Cast<byte, uint>(output.Span);
        ReadOnlySpan<uint> keySpan = key.Span;

        for (int pos = 0; pos < words.Length; pos += 2)
        {
            uint v0 = words[pos];
            uint v1 = words[pos + 1];
            uint sum = 0;

            for (int i = 0; i < NumRounds; i++)
            {
                v0 += (v1 << 4 ^ v1 >> 5) + v1 ^ sum + keySpan[(int)(sum & 3)];
                sum += Delta;
                v1 += (v0 << 4 ^ v0 >> 5) + v0 ^ sum + keySpan[(int)(sum >> 11 & 3)];
            }

            words[pos] = v0;
            words[pos + 1] = v1;
        }
    }

    /// <summary>
    /// Decrypts the specified data using the XTEA algorithm.
    /// </summary>
    /// <param name="data">The data to decrypt.</param>
    /// <param name="key">The decryption key (must be exactly 4 elements).</param>
    /// <param name="output">The buffer to store the decrypted data (must be large enough to hold the result).</param>
    /// <exception cref="ArgumentException">Thrown when the key length is not exactly 4 elements, the data length is not a multiple of 8, or the output buffer is too small.</exception>
    public static void Decrypt(Memory<byte> data, ReadOnlyMemory<uint> key, Memory<byte> output)
    {
        if (data.Length % 8 != 0)
            throw new ArgumentException("Invalid input data or key.", nameof(data));

        if (key.Length != 4)
            throw new ArgumentException("Key must be exactly 4 elements.", nameof(key));

        data.CopyTo(output);

        Span<uint> words = MemoryMarshal.Cast<byte, uint>(output.Span);
        ReadOnlySpan<uint> keySpan = key.Span;

        for (int pos = 0; pos < words.Length; pos += 2)
        {
            uint v0 = words[pos];
            uint v1 = words[pos + 1];
            uint sum = unchecked(Delta * NumRounds);

            for (int i = 0; i < NumRounds; i++)
            {
                v1 -= (v0 << 4 ^ v0 >> 5) + v0 ^ sum + keySpan[(int)(sum >> 11 & 3)];
                sum -= Delta;
                v0 -= (v1 << 4 ^ v1 >> 5) + v1 ^ sum + keySpan[(int)(sum & 3)];
            }

            words[pos] = v0;
            words[pos + 1] = v1;
        }
    }
}
