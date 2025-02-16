using System;
using System.Linq;

namespace Notio.Cryptography.Hash;

/// <summary>
/// Provides an implementation of the SHA-1 hash algorithm.
/// </summary>
public class SHA1
{
    /// <summary>
    /// Computes the SHA-1 hash of the provided data.
    /// </summary>
    /// <param name="data">The data to hash.</param>
    /// <returns>The SHA-1 hash as a byte array.</returns>
    public static byte[] ComputeHash(byte[] data)
    {
        uint[] h = (uint[])_h.Clone();
        byte[] padded = PadMessage(data);
        ProcessBlocks(padded, h);
        return [.. h.SelectMany(BitConverter.GetBytes).Reverse()];
    }

    private static byte[] PadMessage(byte[] message)
    {
        long bitLength = message.Length * 8;
        int paddingLength = (56 - (message.Length + 1) % 64 + 64) % 64;
        byte[] padded = new byte[message.Length + 1 + paddingLength + 8];
        Array.Copy(message, padded, message.Length);
        padded[message.Length] = 0x80;
        Array.Copy(BitConverter.GetBytes(bitLength).Reverse().ToArray(), 0, padded, padded.Length - 8, 8);
        return padded;
    }

    private static void ProcessBlocks(byte[] data, uint[] h)
    {
        for (int i = 0; i < data.Length; i += 64)
        {
            uint[] w = new uint[80];
            for (int j = 0; j < 16; j++)
            {
                w[j] = BitConverter.ToUInt32([.. data.Skip(i + j * 4).Take(4).Reverse()], 0);
            }
            for (int j = 16; j < 80; j++)
            {
                w[j] = RotateLeft(w[j - 3] ^ w[j - 8] ^ w[j - 14] ^ w[j - 16], 1);
            }

            uint a = h[0], b = h[1], c = h[2], d = h[3], e = h[4];

            for (int j = 0; j < 80; j++)
            {
                uint f, k;
                if (j < 20) { f = b & c | ~b & d; k = 0x5A827999; }
                else if (j < 40) { f = b ^ c ^ d; k = 0x6ED9EBA1; }
                else if (j < 60) { f = b & c | b & d | c & d; k = 0x8F1BBCDC; }
                else { f = b ^ c ^ d; k = 0xCA62C1D6; }

                uint temp = RotateLeft(a, 5) + f + e + k + w[j];
                e = d;
                d = c;
                c = RotateLeft(b, 30);
                b = a;
                a = temp;
            }

            h[0] += a;
            h[1] += b;
            h[2] += c;
            h[3] += d;
            h[4] += e;
        }
    }

    private static uint RotateLeft(uint value, int bits) => value << bits | value >> 32 - bits;

    private static readonly uint[] _h =
    [
        0x67452301, 0xEFCDAB89, 0x98BADCFE, 0x10325476, 0xC3D2E1F0
    ];
}
