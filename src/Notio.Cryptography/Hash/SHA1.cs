using Notio.Cryptography.Utilities;
using System;
using System.Linq;

namespace Notio.Cryptography.Hash;

/// <summary>
/// Provides an implementation of the SHA-1 hash algorithm.
/// </summary>
public class SHA1
{
    private static readonly uint[] K =
    [
        0x67452301, 0xEFCDAB89, 0x98BADCFE, 0x10325476, 0xC3D2E1F0
    ];

    /// <summary>
    /// Computes the SHA-1 hash of the provided data.
    /// </summary>
    /// <param name="data">The data to hash.</param>
    /// <returns>The SHA-1 hash as a 20-byte array in big-endian order.</returns>
    public static byte[] ComputeHash(byte[] data)
    {
        uint[] h = (uint[])K.Clone();
        byte[] padded = PadMessage(data);
        ProcessBlocks(padded, h);
        return [.. h.SelectMany(x => BitConverter.GetBytes(BitwiseUtils.ReverseBytes(x)))];
    }

    /// <summary>
    /// Pads the message according to the SHA-1 specification.
    /// </summary>
    /// <param name="message">The original message bytes.</param>
    /// <returns>The padded message.</returns>
    private static byte[] PadMessage(byte[] message)
    {
        long bitLength = message.Length * 8L;
        int paddingLength = (int)((56 - (message.Length + 1) % 64 + 64) % 64);
        byte[] padded = new byte[message.Length + 1 + paddingLength + 8];

        Array.Copy(message, padded, message.Length);

        padded[message.Length] = 0x80;
        byte[] lengthBytes = BitConverter.GetBytes(bitLength);

        if (BitConverter.IsLittleEndian) Array.Reverse(lengthBytes);

        Array.Copy(lengthBytes, 0, padded, padded.Length - 8, 8);
        return padded;
    }

    /// <summary>
    /// Processes the padded message in 512-bit (64-byte) blocks.
    /// </summary>
    /// <param name="data">The padded message.</param>
    /// <param name="h">The current hash state array.</param>
    private static void ProcessBlocks(byte[] data, uint[] h)
    {
        for (int i = 0; i < data.Length; i += 64)
        {
            uint[] w = new uint[80];

            for (int j = 0; j < 16; j++)
            {
                byte[] temp = [.. data.Skip(i + j * 4).Take(4).Reverse()];
                w[j] = BitConverter.ToUInt32(temp, 0);
            }

            for (int j = 16; j < 80; j++)
            {
                w[j] = BitwiseUtils.RotateLeft(w[j - 3] ^ w[j - 8] ^ w[j - 14] ^ w[j - 16], 1);
            }

            uint a = h[0], b = h[1], c = h[2], d = h[3], e = h[4];

            for (int j = 0; j < 80; j++)
            {
                uint f, k;
                if (j < 20)
                {
                    f = (b & c) | ((~b) & d);
                    k = 0x5A827999;
                }
                else if (j < 40)
                {
                    f = b ^ c ^ d;
                    k = 0x6ED9EBA1;
                }
                else if (j < 60)
                {
                    f = (b & c) | (b & d) | (c & d);
                    k = 0x8F1BBCDC;
                }
                else
                {
                    f = b ^ c ^ d;
                    k = 0xCA62C1D6;
                }

                uint temp = BitwiseUtils.RotateLeft(a, 5) + f + e + k + w[j];
                e = d;
                d = c;
                c = BitwiseUtils.RotateLeft(b, 30);
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
}
