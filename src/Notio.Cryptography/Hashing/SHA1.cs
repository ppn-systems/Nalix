using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Notio.Cryptography.Hashing;

/// <summary>
/// Provides an optimized implementation of the SHA-1 cryptographic hash algorithm.
/// </summary>
/// <remarks>
/// SHA-1 is a cryptographic hash function that produces a 160-bit (20-byte) hash value.
/// It is considered weak due to known vulnerabilities but is still used in legacy systems.
/// This implementation processes data in 512-bit (64-byte) blocks.
/// </remarks>
public static class Sha1
{
    /// <summary>
    /// The initial hash values (H0-H4) as defined in the SHA-1 specification.
    /// These values are used as the starting state of the hash computation.
    /// </summary>
    public static readonly uint[] K =
    [
        0x67452301,
        0xEFCDAB89,
        0x98BADCFE,
        0x10325476,
        0xC3D2E1F0
    ];

    /// <summary>
    /// The SHA-1 round constants used in the message expansion and compression functions.
    /// </summary>
    /// <remarks>
    /// There are four constants corresponding to different rounds of the SHA-1 process:
    /// - `0x5A827999` for rounds 0-19
    /// - `0x6ED9EBA1` for rounds 20-39
    /// - `0x8F1BBCDC` for rounds 40-59
    /// - `0xCA62C1D6` for rounds 60-79
    /// </remarks>
    public static readonly uint[] RoundConstants =
    [
        0x5A827999, 0x6ED9EBA1,
        0x8F1BBCDC, 0xCA62C1D6
    ];

    /// <summary>
    /// Computes the SHA-1 hash of the provided data.
    /// </summary>
    /// <param name="data">The input data to hash.</param>
    /// <returns>
    /// The SHA-1 hash as a 20-byte array.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown if the input data is excessively large (greater than 2^61 bytes),
    /// as SHA-1 uses a 64-bit length field.
    /// </exception>
    /// <remarks>
    /// This method follows the standard SHA-1 padding and processing rules:
    /// - The input data is processed in 64-byte blocks.
    /// - A padding byte `0x80` is added after the data.
    /// - The length of the original message (in bits) is appended in big-endian format.
    /// </remarks>
    public static byte[] ComputeHash(ReadOnlySpan<byte> data)
    {
        Span<uint> h = stackalloc uint[5];
        K.CopyTo(h);

        // Calculate message length in bits (before padding)
        ulong bitLength = (ulong)data.Length * 8;

        // Process all complete blocks
        int fullBlocks = data.Length / 64;
        for (int i = 0; i < fullBlocks; i++)
        {
            ProcessBlock(data.Slice(i * 64, 64), h);
        }

        // Handle the final block with padding
        int remainingBytes = data.Length % 64;
        Span<byte> finalBlock = stackalloc byte[128]; // Max 2 blocks needed

        // Copy remaining data to the final block
        data[^remainingBytes..].CopyTo(finalBlock);

        // Add the '1' bit
        finalBlock[remainingBytes] = 0x80;

        // Determine if we need one or two blocks
        int blockCount = (remainingBytes + 1 + 8 > 64) ? 2 : 1;
        int finalBlockSize = blockCount * 64;

        // Write the length in bits as a 64-bit big-endian integer
        BinaryPrimitives.WriteUInt64BigEndian(
            finalBlock[(finalBlockSize - 8)..],
            bitLength);

        // Process the final block(s)
        for (int i = 0; i < blockCount; i++)
        {
            ProcessBlock(finalBlock.Slice(i * 64, 64), h);
        }

        // Convert the hash to bytes in big-endian format
        Span<byte> result = stackalloc byte[20];
        for (int i = 0; i < 5; i++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(result[(i * 4)..], h[i]);
        }

        return result.ToArray();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ProcessBlock(ReadOnlySpan<byte> block, Span<uint> h)
    {
        // Use hardware acceleration if available
        if (Ssse3.IsSupported)
        {
            ProcessBlockSsse3(block, h);
            return;
        }

        ProcessBlockScalar(block, h);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void ProcessBlockScalar(ReadOnlySpan<byte> block, Span<uint> h)
    {
        Span<uint> w = stackalloc uint[80];

        // Load first 16 words from big-endian data
        for (int j = 0; j < 16; j++)
        {
            w[j] = BinaryPrimitives.ReadUInt32BigEndian(block[(j * 4)..]);
        }

        // Message schedule expansion
        for (int j = 16; j < 80; j++)
        {
            w[j] = BitOperations.RotateLeft(w[j - 3] ^ w[j - 8] ^ w[j - 14] ^ w[j - 16], 1);
        }

        // Initialize working variables
        uint a = h[0], b = h[1], c = h[2], d = h[3], e = h[4];

        // Main loop - optimized with function inlining
        for (int j = 0; j < 20; j++)
        {
            uint temp = BitOperations.RotateLeft(a, 5) + ((b & c) | ((~b) & d)) + e + RoundConstants[0] + w[j];
            e = d;
            d = c;
            c = BitOperations.RotateLeft(b, 30);
            b = a;
            a = temp;
        }

        for (int j = 20; j < 40; j++)
        {
            uint temp = BitOperations.RotateLeft(a, 5) + (b ^ c ^ d) + e + RoundConstants[1] + w[j];
            e = d;
            d = c;
            c = BitOperations.RotateLeft(b, 30);
            b = a;
            a = temp;
        }

        for (int j = 40; j < 60; j++)
        {
            uint temp = BitOperations.RotateLeft(a, 5) + ((b & c) | (b & d) | (c & d)) + e + RoundConstants[2] + w[j];
            e = d;
            d = c;
            c = BitOperations.RotateLeft(b, 30);
            b = a;
            a = temp;
        }

        for (int j = 60; j < 80; j++)
        {
            uint temp = BitOperations.RotateLeft(a, 5) + (b ^ c ^ d) + e + RoundConstants[3] + w[j];
            e = d;
            d = c;
            c = BitOperations.RotateLeft(b, 30);
            b = a;
            a = temp;
        }

        // Update hash state
        h[0] += a;
        h[1] += b;
        h[2] += c;
        h[3] += d;
        h[4] += e;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void ProcessBlockSsse3(ReadOnlySpan<byte> block, Span<uint> h)
    {
        // Stack allocation for the message schedule array
        Span<uint> w = stackalloc uint[80];

        fixed (byte* pBlock = block)
        {
            // Shuffle mask for big-endian to little-endian conversion
            var shuffleMask = Vector128.Create(
                3, 2, 1, 0,
                7, 6, 5, 4,
                11, 10, 9, 8,
                15, 14, 13, 12
            ).AsByte();

            // Load and byte-swap 16 words using SIMD
            for (int i = 0; i < 16; i += 4)
            {
                var chunk = Ssse3.Shuffle(Sse2.LoadVector128(pBlock + i * 4), shuffleMask).AsUInt32();
                w[i] = chunk[0];
                w[i + 1] = chunk[1];
                w[i + 2] = chunk[2];
                w[i + 3] = chunk[3];
            }
        }

        // Message schedule expansion with unrolling for better performance
        for (int i = 16; i < 80; i += 4)
        {
            for (int j = 0; j < 4; j++)
            {
                w[i + j] = BitOperations.RotateLeft(w[(i + j) - 3] ^ w[(i + j) - 8] ^ w[(i + j) - 14] ^ w[(i + j) - 16], 1);
            }
        }

        // Initialize working variables
        uint a = h[0], b = h[1], c = h[2], d = h[3], e = h[4];

        // Process rounds with loop unrolling
        for (int j = 0; j < 20; j += 5)
        {
            ProcessRound(ref a, ref b, ref c, ref d, ref e, CH(b, c, d), RoundConstants[0], w[j]);
            ProcessRound(ref e, ref a, ref b, ref c, ref d, CH(a, b, c), RoundConstants[0], w[j + 1]);
            ProcessRound(ref d, ref e, ref a, ref b, ref c, CH(e, a, b), RoundConstants[0], w[j + 2]);
            ProcessRound(ref c, ref d, ref e, ref a, ref b, CH(d, e, a), RoundConstants[0], w[j + 3]);
            ProcessRound(ref b, ref c, ref d, ref e, ref a, CH(c, d, e), RoundConstants[0], w[j + 4]);
        }

        for (int j = 20; j < 40; j += 5)
        {
            ProcessRound(ref a, ref b, ref c, ref d, ref e, PARITY(b, c, d), RoundConstants[1], w[j]);
            ProcessRound(ref e, ref a, ref b, ref c, ref d, PARITY(a, b, c), RoundConstants[1], w[j + 1]);
            ProcessRound(ref d, ref e, ref a, ref b, ref c, PARITY(e, a, b), RoundConstants[1], w[j + 2]);
            ProcessRound(ref c, ref d, ref e, ref a, ref b, PARITY(d, e, a), RoundConstants[1], w[j + 3]);
            ProcessRound(ref b, ref c, ref d, ref e, ref a, PARITY(c, d, e), RoundConstants[1], w[j + 4]);
        }

        for (int j = 40; j < 60; j += 5)
        {
            ProcessRound(ref a, ref b, ref c, ref d, ref e, MAJ(b, c, d), RoundConstants[2], w[j]);
            ProcessRound(ref e, ref a, ref b, ref c, ref d, MAJ(a, b, c), RoundConstants[2], w[j + 1]);
            ProcessRound(ref d, ref e, ref a, ref b, ref c, MAJ(e, a, b), RoundConstants[2], w[j + 2]);
            ProcessRound(ref c, ref d, ref e, ref a, ref b, MAJ(d, e, a), RoundConstants[2], w[j + 3]);
            ProcessRound(ref b, ref c, ref d, ref e, ref a, MAJ(c, d, e), RoundConstants[2], w[j + 4]);
        }

        for (int j = 60; j < 80; j += 5)
        {
            ProcessRound(ref a, ref b, ref c, ref d, ref e, PARITY(b, c, d), RoundConstants[3], w[j]);
            ProcessRound(ref e, ref a, ref b, ref c, ref d, PARITY(a, b, c), RoundConstants[3], w[j + 1]);
            ProcessRound(ref d, ref e, ref a, ref b, ref c, PARITY(e, a, b), RoundConstants[3], w[j + 2]);
            ProcessRound(ref c, ref d, ref e, ref a, ref b, PARITY(d, e, a), RoundConstants[3], w[j + 3]);
            ProcessRound(ref b, ref c, ref d, ref e, ref a, PARITY(c, d, e), RoundConstants[3], w[j + 4]);
        }

        // Update hash state
        h[0] += a;
        h[1] += b;
        h[2] += c;
        h[3] += d;
        h[4] += e;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ProcessRound(ref uint a, ref uint b, ref uint c, ref uint d, ref uint e, uint f, uint k, uint w)
    {
        uint temp = BitOperations.RotateLeft(a, 5) + f + e + k + w;
        e = d;
        d = c;
        c = BitOperations.RotateLeft(b, 30);
        b = a;
        a = temp;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint CH(uint x, uint y, uint z) => (x & y) ^ ((~x) & z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint PARITY(uint x, uint y, uint z) => x ^ y ^ z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint MAJ(uint x, uint y, uint z) => (x & y) ^ (x & z) ^ (y & z);

    // Helper class to provide bit operations if not using .NET Core 3.0+
    private static class BitOperations
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint RotateLeft(uint value, int offset)
        {
            return (value << offset) | (value >> (32 - offset));
        }
    }
}
