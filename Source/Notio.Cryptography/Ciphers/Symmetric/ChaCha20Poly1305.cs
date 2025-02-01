using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Notio.Cryptography.Ciphers.Symmetric;

public class ChaCha20Poly1305
{
    // ----------------------------
    // Public API: Encrypt and Decrypt
    // ----------------------------

    /// <summary>
    /// Encrypts the plaintext using ChaCha20-Poly1305.
    /// </summary>
    /// <param name="key">A 32-byte key.</param>
    /// <param name="nonce">A 12-byte nonce.</param>
    /// <param name="plaintext">The plaintext to encrypt.</param>
    /// <param name="aad">Additional authenticated data (AAD) – can be empty.</param>
    /// <param name="ciphertext">Output: the resulting ciphertext.</param>
    /// <param name="tag">Output: the authentication tag (16 bytes).</param>
    public static void Encrypt(byte[] key, byte[] nonce, byte[] plaintext, byte[] aad, out byte[] ciphertext, out byte[] tag)
    {
        if (key.Length != 32)
            throw new ArgumentException("Key must be 32 bytes.");
        if (nonce.Length != 12)
            throw new ArgumentException("Nonce must be 12 bytes.");

        // Generate a one-time Poly1305 key using ChaCha20 block with counter 0.
        byte[] chachaBlock0 = ChaCha20Block(key, nonce, 0);
        byte[] polyKey = new byte[32];
        Array.Copy(chachaBlock0, polyKey, 32);

        // Encrypt plaintext using ChaCha20 with counter starting at 1.
        ciphertext = new byte[plaintext.Length];
        int blockCount = (plaintext.Length + 63) / 64; // 64-byte blocks
        for (int i = 0; i < blockCount; i++)
        {
            uint counter = (uint)(i + 1);
            byte[] keystream = ChaCha20Block(key, nonce, counter);
            int offset = i * 64;
            int blockSize = Math.Min(64, plaintext.Length - offset);
            for (int j = 0; j < blockSize; j++)
            {
                ciphertext[offset + j] = (byte)(plaintext[offset + j] ^ keystream[j]);
            }
        }
        // Compute the Poly1305 authentication tag.
        tag = Poly1305Compute(polyKey, aad, ciphertext);
    }

    /// <summary>
    /// Decrypts the ciphertext using ChaCha20-Poly1305.
    /// </summary>
    /// <param name="key">A 32-byte key.</param>
    /// <param name="nonce">A 12-byte nonce.</param>
    /// <param name="ciphertext">The ciphertext to decrypt.</param>
    /// <param name="aad">Additional authenticated data (AAD) – must be the same as during encryption.</param>
    /// <param name="tag">The authentication tag (16 bytes) to verify.</param>
    /// <param name="plaintext">Output: the resulting plaintext if authentication succeeds.</param>
    /// <returns>True if authentication passes and decryption is successful; otherwise, false.</returns>
    public static bool Decrypt(byte[] key, byte[] nonce, byte[] ciphertext, byte[] aad, byte[] tag, out byte[] plaintext)
    {
        plaintext = new byte[ciphertext.Length];
        if (key.Length != 32)
            throw new ArgumentException("Key must be 32 bytes.");
        if (nonce.Length != 12)
            throw new ArgumentException("Nonce must be 12 bytes.");

        // Regenerate the one-time Poly1305 key.
        byte[] chachaBlock0 = ChaCha20Block(key, nonce, 0);
        byte[] polyKey = new byte[32];
        Array.Copy(chachaBlock0, polyKey, 32);

        // Compute the expected tag.
        byte[] computedTag = Poly1305Compute(polyKey, aad, ciphertext);
        // Constant-time comparison is recommended in production.
        if (!computedTag.SequenceEqual(tag))
        {
            return false; // Authentication failed.
        }

        // Decrypt using ChaCha20 with counter starting at 1.
        int blockCount = (ciphertext.Length + 63) / 64;
        for (int i = 0; i < blockCount; i++)
        {
            uint counter = (uint)(i + 1);
            byte[] keystream = ChaCha20Block(key, nonce, counter);
            int offset = i * 64;
            int blockSize = Math.Min(64, ciphertext.Length - offset);
            for (int j = 0; j < blockSize; j++)
            {
                plaintext[offset + j] = (byte)(ciphertext[offset + j] ^ keystream[j]);
            }
        }
        return true;
    }

    // ----------------------------
    // ChaCha20 Implementation
    // ----------------------------

    // Rotate left
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Rotl(uint x, int n) => x << n | x >> 32 - n;

    // ChaCha20 quarter round
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void QuarterRound(ref uint a, ref uint b, ref uint c, ref uint d)
    {
        a += b; d ^= a; d = Rotl(d, 16);
        c += d; b ^= c; b = Rotl(b, 12);
        a += b; d ^= a; d = Rotl(d, 8);
        c += d; b ^= c; b = Rotl(b, 7);
    }

    // Generates a 64-byte ChaCha20 keystream block using the given key, nonce, and counter.
    // key must be 32 bytes; nonce must be 12 bytes.
    private static byte[] ChaCha20Block(byte[] key, byte[] nonce, uint counter)
    {
        if (key.Length != 32)
            throw new ArgumentException("Key must be 32 bytes.");
        if (nonce.Length != 12)
            throw new ArgumentException("Nonce must be 12 bytes.");

        // Initialize state with constants, key, counter, and nonce.
        uint[] state = new uint[16];
        state[0] = 0x61707865; // "expa"
        state[1] = 0x3320646e; // "nd 3"
        state[2] = 0x79622d32; // "2-by"
        state[3] = 0x6b206574; // "te k"
        for (int i = 0; i < 8; i++)
        {
            state[4 + i] = BitConverter.ToUInt32(key, i * 4);
        }
        state[12] = counter;
        for (int i = 0; i < 3; i++)
        {
            state[13 + i] = BitConverter.ToUInt32(nonce, i * 4);
        }

        // Copy state to workingState and perform 20 rounds (10 double rounds)
        uint[] workingState = (uint[])state.Clone();
        for (int i = 0; i < 10; i++)
        {
            // Column rounds
            QuarterRound(ref workingState[0], ref workingState[4], ref workingState[8], ref workingState[12]);
            QuarterRound(ref workingState[1], ref workingState[5], ref workingState[9], ref workingState[13]);
            QuarterRound(ref workingState[2], ref workingState[6], ref workingState[10], ref workingState[14]);
            QuarterRound(ref workingState[3], ref workingState[7], ref workingState[11], ref workingState[15]);
            // Diagonal rounds
            QuarterRound(ref workingState[0], ref workingState[5], ref workingState[10], ref workingState[15]);
            QuarterRound(ref workingState[1], ref workingState[6], ref workingState[11], ref workingState[12]);
            QuarterRound(ref workingState[2], ref workingState[7], ref workingState[8], ref workingState[13]);
            QuarterRound(ref workingState[3], ref workingState[4], ref workingState[9], ref workingState[14]);
        }

        // Add the original state to the working state and serialize to a byte array.
        byte[] output = new byte[64];
        for (int i = 0; i < 16; i++)
        {
            uint result = workingState[i] + state[i];
            byte[] bytes = BitConverter.GetBytes(result);
            Array.Copy(bytes, 0, output, i * 4, 4);
        }
        return output;
    }

    // ----------------------------
    // Poly1305 Implementation
    // ----------------------------

    // Computes the Poly1305 MAC. The MAC is computed over:
    //   AAD || (padding) || Ciphertext || (padding) || [AAD length (8 bytes)] || [Ciphertext length (8 bytes)]
    private static byte[] Poly1305Compute(byte[] polyKey, byte[] aad, byte[] ciphertext)
    {
        // Build the message for MAC.
        List<byte> m = [.. aad];
        if (aad.Length % 16 != 0)
        {
            m.AddRange(new byte[16 - aad.Length % 16]);
        }
        m.AddRange(ciphertext);
        if (ciphertext.Length % 16 != 0)
        {
            m.AddRange(new byte[16 - ciphertext.Length % 16]);
        }
        m.AddRange(BitConverter.GetBytes((ulong)aad.Length));
        m.AddRange(BitConverter.GetBytes((ulong)ciphertext.Length));
        byte[] msg = [.. m];

        // Clamp r (the first 16 bytes of polyKey) as per RFC 8439.
        byte[] rBytes = new byte[16];
        Array.Copy(polyKey, 0, rBytes, 0, 16);
        rBytes[3] &= 0x0f;
        rBytes[7] &= 0x0f;
        rBytes[11] &= 0x0f;
        rBytes[15] &= 0x0f;
        // Append an extra zero to ensure the BigInteger is non-negative.
        byte[] rExtended = new byte[rBytes.Length + 1];
        Array.Copy(rBytes, rExtended, rBytes.Length);
        BigInteger r = new(rExtended);

        // s is the remaining 16 bytes of polyKey.
        byte[] sBytes = new byte[16];
        Array.Copy(polyKey, 16, sBytes, 0, 16);
        byte[] sExtended = new byte[sBytes.Length + 1];
        Array.Copy(sBytes, sExtended, sBytes.Length);
        BigInteger s = new(sExtended);

        BigInteger accumulator = BigInteger.Zero;
        BigInteger p = (BigInteger.One << 130) - 5;
        // Process the message in 16-byte blocks.
        for (int i = 0; i < msg.Length; i += 16)
        {
            int blockSize = Math.Min(16, msg.Length - i);
            // Create a block and append a 0x01 byte.
            byte[] block = new byte[blockSize + 1];
            Array.Copy(msg, i, block, 0, blockSize);
            block[blockSize] = 0x01;
            // Append an extra zero to ensure non-negativity.
            byte[] blockExtended = new byte[block.Length + 1];
            Array.Copy(block, blockExtended, block.Length);
            BigInteger n = new(blockExtended);
            accumulator = (accumulator + n) % p;
            accumulator = accumulator * r % p;
        }
        accumulator = (accumulator + s) % (BigInteger.One << 128);
        byte[] tag = accumulator.ToByteArray();

        // Ensure the tag is exactly 16 bytes.
        if (tag.Length < 16)
        {
            Array.Resize(ref tag, 16);
        }
        else if (tag.Length > 16)
        {
            tag = tag.Take(16).ToArray();
        }
        return tag;
    }
}