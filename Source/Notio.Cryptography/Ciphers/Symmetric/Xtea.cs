using System;

namespace Notio.Cryptography.Ciphers.Symmetric;

public static class Xtea
{
    public static unsafe byte[] Encrypt(byte[] data, uint[] key)
    {
        if (key == null || key.Length < 4)
            throw new ArgumentException("Key must be at least 4 elements", nameof(key));

        int originalLength = data.Length;
        int pad = originalLength % 8;
        if (pad != 0)
            Array.Resize(ref data, originalLength + (8 - pad));

        uint[] words = new uint[data.Length / 4];
        Buffer.BlockCopy(data, 0, words, 0, data.Length);

        fixed (uint* wordsPtr = words, keyPtr = key)
        {
            uint delta = 0x9E3779B9;
            for (int pos = 0; pos < words.Length; pos += 2)
            {
                uint* v0 = wordsPtr + pos;
                uint* v1 = v0 + 1;
                uint sum = 0;

                for (int i = 0; i < 32; i++)
                {
                    *v0 += (*v1 << 4 ^ *v1 >> 5) + *v1 ^ sum + keyPtr[sum & 3];
                    sum += delta;
                    *v1 += (*v0 << 4 ^ *v0 >> 5) + *v0 ^ sum + keyPtr[sum >> 11 & 3];
                }
            }
        }

        byte[] encryptedData = new byte[words.Length * 4];
        Buffer.BlockCopy(words, 0, encryptedData, 0, encryptedData.Length);
        return encryptedData;
    }

    public static unsafe bool Decrypt(byte[] data, uint[] key)
    {
        if (data == null || key == null || key.Length < 4 || data.Length % 8 != 0)
            return false;

        uint[] words = new uint[data.Length / 4];
        Buffer.BlockCopy(data, 0, words, 0, data.Length);

        fixed (uint* wordsPtr = words, keyPtr = key)
        {
            uint delta = 0x9E3779B9;
            for (int pos = 0; pos < words.Length; pos += 2)
            {
                uint* v0 = wordsPtr + pos;
                uint* v1 = v0 + 1;
                uint sum = 0xC6EF3720;

                for (int i = 0; i < 32; i++)
                {
                    *v1 -= (*v0 << 4 ^ *v0 >> 5) + *v0 ^ sum + keyPtr[sum >> 11 & 3];
                    sum -= delta;
                    *v0 -= (*v1 << 4 ^ *v1 >> 5) + *v1 ^ sum + keyPtr[sum & 3];
                }
            }
        }

        Buffer.BlockCopy(words, 0, data, 0, data.Length);
        return true;
    }
}