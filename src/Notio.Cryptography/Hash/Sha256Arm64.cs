using System;
using System.Linq;
using System.Text;

namespace Notio.Cryptography.Hash;

public static class Sha256
{
    private static readonly uint[] K = [
        0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
        0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
        0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
        0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
        0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
        0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
        0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
        0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2];

    public static string Result(string message)
    {
        byte[] paddedMessage = Padding(message);
        string hash = Hashing(paddedMessage);

        return hash;
    }

    public static byte[] Padding(string originalMessage)
    {
        byte[] messageBytes = Encoding.UTF8.GetBytes(originalMessage);
        ulong bitLength = (ulong)(messageBytes.Length * 8);

        // Thêm bit '1'
        int totalLength = ((messageBytes.Length + 9 + 63) / 64) * 64;
        byte[] paddedMessage = new byte[totalLength];
        Array.Copy(messageBytes, paddedMessage, messageBytes.Length);
        paddedMessage[messageBytes.Length] = 0x80;

        // Ghi độ dài message (big-endian) vào 8 byte cuối
        for (int i = 0; i < 8; i++)
        {
            paddedMessage[paddedMessage.Length - 1 - i] = (byte)(bitLength >> (i * 8));
        }

        return paddedMessage;
    }

    public static string Hashing(byte[] paddedMessage)
    {
        int numOfChunks = paddedMessage.Length / 64;
        uint[] hash = { 0x6a09e667, 0xbb67ae85, 0x3c6ef372, 0xa54ff53a,
                    0x510e527f, 0x9b05688c, 0x1f83d9ab, 0x5be0cd19 };

        for (int k = 0; k < numOfChunks; k++)
        {
            uint[] W = new uint[64];
            for (int m = 0; m < 16; m++)
                W[m] = BitConverter.ToUInt32(paddedMessage, k * 64 + m * 4).ReverseEndian();

            for (int n = 16; n < 64; n++)
                W[n] = W[n - 16] + sigma0(W[n - 15]) + W[n - 7] + sigma1(W[n - 2]);

            uint a = hash[0], b = hash[1], c = hash[2], d = hash[3];
            uint e = hash[4], f = hash[5], g = hash[6], h = hash[7];

            for (int p = 0; p < 64; p++)
            {
                uint temp1 = h + Sigma1(e) + Choice(e, f, g) + K[p] + W[p];
                uint temp2 = Sigma0(a) + Majority(a, b, c);
                h = g; g = f; f = e; e = d + temp1;
                d = c; c = b; b = a; a = temp1 + temp2;
            }

            hash[0] += a; hash[1] += b; hash[2] += c; hash[3] += d;
            hash[4] += e; hash[5] += f; hash[6] += g; hash[7] += h;
        }

        return string.Concat(hash.Select(h => h.ToString("X8")));
    }

    public static uint RightRotate(uint val, int numOfBits)
    {
        return val >> numOfBits | val << 32 - numOfBits;
    }

    public static uint RightShift(uint val, int numOfBits)
    {
        return val >> numOfBits;
    }

    public static uint Majority(uint x, uint y, uint z)
    {
        return x & y ^ x & z ^ y & z;
    }

    public static uint sigma0(uint x)
    {
        return RightRotate(x, 7) ^ RightRotate(x, 18) ^ RightShift(x, 3);
    }

    public static uint sigma1(uint x)
    {
        return RightRotate(x, 17) ^ RightRotate(x, 19) ^ RightShift(x, 10);
    }

    public static uint Sigma0(uint x)
    {
        return RightRotate(x, 2) ^ RightRotate(x, 13) ^ RightRotate(x, 22);
    }

    public static uint Sigma1(uint x)
    {
        return RightRotate(x, 6) ^ RightRotate(x, 11) ^ RightRotate(x, 25);
    }

    public static uint Choice(uint x, uint y, uint z)
    {
        return x & y ^ ~x & z;
    }

    public static T[] SubArray<T>(T[] data, int index, int length)
    {
        T[] result = new T[length];
        Array.Copy(data, index, result, 0, length);
        return result;
    }
}
