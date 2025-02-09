using System;
using System.Buffers.Binary;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Notio.Cryptography.Ciphers.Symmetric;

/// <summary>
/// Provides encryption and decryption utilities using the ChaCha20 stream cipher combined with Poly1305 for message authentication.
/// ChaCha20Poly1305 is an authenticated encryption algorithm providing both confidentiality and integrity.
/// </summary>
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
    public static void Encrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> aad,
        out byte[] ciphertext, out byte[] tag)
    {
        if (key.Length != 32)
            throw new ArgumentException("Key must be 32 bytes.");
        if (nonce.Length != 12)
            throw new ArgumentException("Nonce must be 12 bytes.");

        // Generate a one-time Poly1305 key using ChaCha20 block with counter 0.
        Span<byte> chachaBlock0 = stackalloc byte[64];
        ChaCha20Block(key, nonce, 0, chachaBlock0);
        Span<byte> polyKey = chachaBlock0[..32]; // polyKey is the first 32 bytes

        // Encrypt plaintext using ChaCha20 with counter starting at 1.
        ciphertext = new byte[plaintext.Length];
        Span<byte> ciphertextSpan = ciphertext;
        int blockCount = (plaintext.Length + 63) / 64;
        Span<byte> keystream = stackalloc byte[64];
        for (int i = 0; i < blockCount; i++)
        {
            uint counter = (uint)(i + 1);
            ChaCha20Block(key, nonce, counter, keystream);
            int offset = i * 64;
            int blockSize = Math.Min(64, plaintext.Length - offset);
            for (int j = 0; j < blockSize; j++)
            {
                ciphertextSpan[offset + j] = (byte)(plaintext[offset + j] ^ keystream[j]);
            }
        }

        tag = Poly1305Compute(polyKey.ToArray(), aad.ToArray(), ciphertext);
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
    public static bool Decrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> aad, ReadOnlySpan<byte> tag,
        out byte[] plaintext)
    {
        plaintext = new byte[ciphertext.Length];
        if (key.Length != 32)
            throw new ArgumentException("Key must be 32 bytes.");
        if (nonce.Length != 12)
            throw new ArgumentException("Nonce must be 12 bytes.");

        Span<byte> chachaBlock0 = stackalloc byte[64];
        ChaCha20Block(key, nonce, 0, chachaBlock0);
        Span<byte> polyKey = chachaBlock0[..32];

        byte[] computedTag = Poly1305Compute(polyKey.ToArray(), aad.ToArray(), ciphertext.ToArray());
        if (!computedTag.AsSpan().SequenceEqual(tag))
        {
            return false; // Authentication failed.
        }

        Span<byte> plaintextSpan = plaintext;
        int blockCount = (ciphertext.Length + 63) / 64;
        Span<byte> keystream = stackalloc byte[64];
        for (int i = 0; i < blockCount; i++)
        {
            uint counter = (uint)(i + 1);
            ChaCha20Block(key, nonce, counter, keystream);
            int offset = i * 64;
            int blockSize = Math.Min(64, ciphertext.Length - offset);
            for (int j = 0; j < blockSize; j++)
            {
                plaintextSpan[offset + j] = (byte)(ciphertext[offset + j] ^ keystream[j]);
            }
        }
        return true;
    }

    // ----------------------------
    // ChaCha20 Implementation
    // ----------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Rotl(uint x, int n) => x << n | x >> (32 - n);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void QuarterRound(ref uint a, ref uint b, ref uint c, ref uint d)
    {
        a += b; d ^= a; d = Rotl(d, 16);
        c += d; b ^= c; b = Rotl(b, 12);
        a += b; d ^= a; d = Rotl(d, 8);
        c += d; b ^= c; b = Rotl(b, 7);
    }

    /// <summary>
    /// Generates a 64-byte ChaCha20 keystream block and writes the output into the provided span.
    /// </summary>
    /// <param name="key">A 32-byte key.</param>
    /// <param name="nonce">A 12-byte nonce.</param>
    /// <param name="counter">The block counter.</param>
    /// <param name="output">A span of length 64 where the keystream will be written.</param>
    private static void ChaCha20Block(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce, uint counter, Span<byte> output)
    {
        if (key.Length != 32)
            throw new ArgumentException("Key must be 32 bytes.");
        if (nonce.Length != 12)
            throw new ArgumentException("Nonce must be 12 bytes.");
        Span<uint> state = stackalloc uint[16];
        state[0] = 0x61707865; // "expa"
        state[1] = 0x3320646e; // "nd 3"
        state[2] = 0x79622d32; // "2-by"
        state[3] = 0x6b206574; // "te k"
        for (int i = 0; i < 8; i++)
        {
            state[4 + i] = BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(i * 4, 4));
        }
        state[12] = counter;
        for (int i = 0; i < 3; i++)
        {
            state[13 + i] = BinaryPrimitives.ReadUInt32LittleEndian(nonce.Slice(i * 4, 4));
        }

        // Copy state to workingState
        uint[] workingState = new uint[16];
        for (int i = 0; i < 16; i++)
            workingState[i] = state[i];

        // 20 rounds (10 double rounds)
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

        // Add the original state and serialize the result to output.
        for (int i = 0; i < 16; i++)
        {
            uint result = workingState[i] + state[i];
            BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(i * 4, 4), result);
        }
    }

    // ----------------------------
    // Poly1305 Implementation
    // ----------------------------

    /// <summary>
    /// Computes the Poly1305 MAC.
    /// The MAC is computed over:
    ///   AAD || (padding) || Ciphertext || (padding) || [AAD length (8 bytes)] || [Ciphertext length (8 bytes)]
    /// </summary>
    /// <param name="polyKey">A 32-byte poly1305 key.</param>
    /// <param name="aad">Additional authenticated data.</param>
    /// <param name="ciphertext">The ciphertext to authenticate.</param>
    /// <returns>A 16-byte authentication tag.</returns>
    private static byte[] Poly1305Compute(byte[] polyKey, byte[] aad, byte[] ciphertext)
    {
        // Calculate necessary padding for AAD and ciphertext.
        int aadPad = (16 - (aad.Length % 16)) % 16;
        int ctPad = (16 - (ciphertext.Length % 16)) % 16;
        int totalLength = aad.Length + aadPad + ciphertext.Length + ctPad + 16; // 16 bytes for AAD and CT lengths (8 each)
        byte[] msg = new byte[totalLength];
        int offset = 0;
        Array.Copy(aad, 0, msg, offset, aad.Length);
        offset += aad.Length;
        offset += aadPad; // padding bytes are already zero
        Array.Copy(ciphertext, 0, msg, offset, ciphertext.Length);
        offset += ciphertext.Length;
        offset += ctPad;
        // Append lengths (each 8 bytes little-endian)
        BitConverter.GetBytes((ulong)aad.Length).CopyTo(msg, offset);
        offset += 8;
        BitConverter.GetBytes((ulong)ciphertext.Length).CopyTo(msg, offset);

        // Clamp r as per RFC 8439.
        Span<byte> rBytes = polyKey.AsSpan(0, 16).ToArray(); // create a copy for modification
        rBytes[3] &= 0x0f;
        rBytes[7] &= 0x0f;
        rBytes[11] &= 0x0f;
        rBytes[15] &= 0x0f;
        byte[] rExtended = new byte[rBytes.Length + 1];
        rBytes.CopyTo(rExtended);
        BigInteger r = new(rExtended);

        byte[] sBytes = new byte[16];
        Array.Copy(polyKey, 16, sBytes, 0, 16);
        byte[] sExtended = new byte[sBytes.Length + 1];
        Array.Copy(sBytes, sExtended, sBytes.Length);
        BigInteger s = new(sExtended);

        BigInteger accumulator = BigInteger.Zero;
        BigInteger p = (BigInteger.One << 130) - 5;
        for (int i = 0; i < msg.Length; i += 16)
        {
            int blockSize = Math.Min(16, msg.Length - i);
            byte[] block = new byte[blockSize + 1];
            Array.Copy(msg, i, block, 0, blockSize);
            block[blockSize] = 0x01;
            byte[] blockExtended = new byte[block.Length + 1];
            Array.Copy(block, blockExtended, block.Length);
            BigInteger n = new(blockExtended);
            accumulator = (accumulator + n) % p;
            accumulator = accumulator * r % p;
        }
        accumulator = (accumulator + s) % (BigInteger.One << 128);
        byte[] tag = accumulator.ToByteArray();
        if (tag.Length < 16)
        {
            Array.Resize(ref tag, 16);
        }
        else if (tag.Length > 16)
        {
            tag = tag.AsSpan(0, 16).ToArray();
        }
        return tag;
    }
}