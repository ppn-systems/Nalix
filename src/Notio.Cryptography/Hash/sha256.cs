using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace Notio.Cryptography.Hash;

/// <summary>
/// Provides an optimized implementation of the SHA-256 cryptographic hash algorithm.
/// </summary>
public sealed class SHA256 : IDisposable
{
    private static readonly uint[] K =
    [
        0x428A2F98, 0x71374491, 0xB5C0FBCF, 0xE9B5DBA5,
        0x3956C25B, 0x59F111F1, 0x923F82A4, 0xAB1C5ED5,
        0xD807AA98, 0x12835B01, 0x243185BE, 0x550C7DC3,
        0x72BE5D74, 0x80DEB1FE, 0x9BDC06A7, 0xC19BF174,
        0xE49B69C1, 0xEFBE4786, 0x0FC19DC6, 0x240CA1CC,
        0x2DE92C6F, 0x4A7484AA, 0x5CB0A9DC, 0x76F988DA,
        0x983E5152, 0xA831C66D, 0xB00327C8, 0xBF597FC7,
        0xC6E00BF3, 0xD5A79147, 0x06CA6351, 0x14292967,
        0x27B70A85, 0x2E1B2138, 0x4D2C6DFC, 0x53380D13,
        0x650A7354, 0x766A0ABB, 0x81C2C92E, 0x92722C85,
        0xA2BFE8A1, 0xA81A664B, 0xC24B8B70, 0xC76C51A3,
        0xD192E819, 0xD6990624, 0xF40E3585, 0x106AA070,
        0x19A4C116, 0x1E376C08, 0x2748774C, 0x34B0BCB5,
        0x391C0CB3, 0x4ED8AA4A, 0x5B9CCA4F, 0x682E6FF3,
        0x748F82EE, 0x78A5636F, 0x84C87814, 0x8CC70208,
        0x90BEFFFA, 0xA4506CEB, 0xBEF9A3F7, 0xC67178F2
    ];

    private readonly uint[] _state = new uint[8];
    private readonly byte[] _buffer = new byte[64];
    private int _bufferLength;
    private ulong _byteCount;
    private bool _finalized;
    private byte[] _finalHash;

    /// <summary>
    /// Initializes a new instance of the <see cref="SHA256"/> class.
    /// </summary>
    public SHA256() => Initialize();

    /// <summary>
    /// Initializes the hash state.
    /// </summary>
    public void Initialize()
    {
        _state[0] = 0x6A09E667;
        _state[1] = 0xBB67AE85;
        _state[2] = 0x3C6EF372;
        _state[3] = 0xA54FF53A;
        _state[4] = 0x510E527F;
        _state[5] = 0x9B05688C;
        _state[6] = 0x1F83D9AB;
        _state[7] = 0x5BE0CD19;
        _bufferLength = 0;
        _byteCount = 0;
        _finalized = false;
        _finalHash = null;
    }

    /// <summary>
    /// Computes the SHA-256 hash of the given data.
    /// </summary>
    /// <param name="data">The input data to hash.</param>
    /// <returns>The computed hash.</returns>
    public static byte[] HashData(ReadOnlySpan<byte> data)
    {
        using SHA256 sha256 = new();
        sha256.Update(data);
        return sha256.FinalizeHash();
    }

    /// <summary>
    /// Computes the SHA-256 hash of the given data.
    /// </summary>
    /// <param name="data">The input data to hash.</param>
    /// <returns>The computed hash.</returns>
    public byte[] ComputeHash(ReadOnlySpan<byte> data)
    {
        Update(data);
        return FinalizeHash();
    }

    /// <summary>
    /// Updates the hash computation with the given data.
    /// </summary>
    /// <param name="data">The data to process.</param>
    /// <exception cref="InvalidOperationException">Thrown if the hash has already been finalized.</exception>
    public void Update(ReadOnlySpan<byte> data)
    {
        if (_finalized)
            throw new InvalidOperationException("Cannot update after finalization.");

        _byteCount += (ulong)data.Length;
    }

    /// <summary>
    /// Finalizes the hash computation and returns the result.
    /// </summary>
    /// <returns>The final hash value.</returns>
    public byte[] FinalizeHash()
    {
        if (_finalized)
            throw new InvalidOperationException("Hash already finalized.");

        // Tính toán số byte padding cần thiết
        int padLength = (_byteCount % 64 < 56) ? (55 - (int)(_byteCount % 64)) : (119 - (int)(_byteCount % 64));

        // Tạo padding buffer
        Span<byte> padding = stackalloc byte[64];
        padding[0] = 0x80; // Bit 1 đầu tiên
        padding.Slice(1, padLength).Clear(); // Phần còn lại là 0

        // Thêm độ dài message vào 8 bytes cuối
        Span<byte> lengthBytes = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(lengthBytes, _byteCount * 8);

        // Update với toàn bộ padding và length
        Update(padding[..(padLength + 1)]);
        Update(lengthBytes);

        _finalized = true;
        byte[] finalHash = new byte[32];

        // Chuyển state thành bytes theo big-endian
        for (int i = 0; i < 8; i++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(finalHash.AsSpan(i * 4), _state[i]);
        }

        return finalHash;
    }

    /// <summary>
    /// Gets the computed hash value after the finalization.
    /// Throws an exception if the hash has not been finalized.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when trying to access the hash before finalization.</exception>
    /// <returns>A byte array containing the hash value.</returns>
    public byte[] Hash
    {
        get
        {
            if (!_finalized)
                throw new InvalidOperationException(
                    "The hash has not been completed. Calling TransformFinalBlock before accessing the Hash.");
            return (byte[])_finalHash.Clone();
        }
    }

    /// <summary>
    /// Updates the hash state with a block of data and returns the number of bytes processed.
    /// This method is similar to the TransformBlock method of the HashAlgorithm class.
    /// </summary>
    /// <param name="inputBuffer">The input byte array containing data to be processed.</param>
    /// <param name="inputOffset">The offset in the inputBuffer to start processing from.</param>
    /// <param name="inputCount">The number of bytes to process from the inputBuffer.</param>
    /// <param name="outputBuffer">The output byte array to store processed data (can be null).</param>
    /// <param name="outputOffset">The offset in the outputBuffer to start writing to.</param>
    /// <returns>The number of bytes processed from the inputBuffer.</returns>
    public int TransformBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset)
    {
        byte[] temp = new byte[inputCount];
        Buffer.BlockCopy(inputBuffer, inputOffset, temp, 0, inputCount);
        Update(temp);

        if (outputBuffer != null)
            Buffer.BlockCopy(inputBuffer, inputOffset, outputBuffer, outputOffset, inputCount);

        return inputCount;
    }

    /// <summary>
    /// Updates the final block of data and completes the hash computation.
    /// This method is similar to the TransformFinalBlock method of the HashAlgorithm class.
    /// </summary>
    /// <param name="inputBuffer">The input byte array containing data to be processed.</param>
    /// <param name="inputOffset">The offset in the inputBuffer to start processing from.</param>
    /// <param name="inputCount">The number of bytes to process from the inputBuffer.</param>
    /// <returns>The final block of processed data.</returns>
    public byte[] TransformFinalBlock(byte[] inputBuffer, int inputOffset, int inputCount)
    {
        byte[] finalBlock = new byte[inputCount];
        Buffer.BlockCopy(inputBuffer, inputOffset, finalBlock, 0, inputCount);
        Update(finalBlock);
        _finalHash = FinalizeHash();
        return finalBlock;
    }

    /// <inheritdoc />
    public void Dispose() => Initialize();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProcessBlock(ReadOnlySpan<byte> block)
    {
        unsafe
        {
            uint* W = stackalloc uint[64];
            uint a = _state[0], b = _state[1], c = _state[2], d = _state[3];
            uint e = _state[4], f = _state[5], g = _state[6], h = _state[7];

            for (int i = 0; i < 16; i++)
            {
                W[i] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(block[(i * 4)..]);
            }

            for (int i = 16; i < 64; i++)
            {
                uint s0 = RotateRight(W[i - 15], 7) ^ RotateRight(W[i - 15], 18) ^ (W[i - 15] >> 3);
                uint s1 = RotateRight(W[i - 2], 17) ^ RotateRight(W[i - 2], 19) ^ (W[i - 2] >> 10);
                W[i] = W[i - 16] + s0 + W[i - 7] + s1;
            }

            // Main computation loop unrolled x4
            for (int i = 0; i < 64;)
            {
                Round(ref a, ref b, ref c, ref d, ref e, ref f, ref g, ref h, W[i], K[i]); i++;
                Round(ref h, ref a, ref b, ref c, ref d, ref e, ref f, ref g, W[i], K[i]); i++;
                Round(ref g, ref h, ref a, ref b, ref c, ref d, ref e, ref f, W[i], K[i]); i++;
                Round(ref f, ref g, ref h, ref a, ref b, ref c, ref d, ref e, W[i], K[i]); i++;
            }

            _state[0] += a; _state[1] += b; _state[2] += c; _state[3] += d;
            _state[4] += e; _state[5] += f; _state[6] += g; _state[7] += h;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Round(
    ref uint a, ref uint b, ref uint c, ref uint d,
    ref uint e, ref uint f, ref uint g, ref uint h,
    uint w, uint k)
    {
        uint s1 = RotateRight(e, 6) ^ RotateRight(e, 11) ^ RotateRight(e, 25);
        uint ch = (e & f) ^ (~e & g);
        uint temp1 = h + s1 + ch + k + w;

        uint s0 = RotateRight(a, 2) ^ RotateRight(a, 13) ^ RotateRight(a, 22);
        uint maj = (a & b) ^ (a & c) ^ (b & c);
        uint temp2 = s0 + maj;

        h = g;
        g = f;
        f = e;
        e = d + temp1;
        d = c;
        c = b;
        b = a;
        a = temp1 + temp2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint RotateRight(uint value, int bits)
    => (value >> bits) | (value << (32 - bits));

    private static void ThrowAlreadyFinalized()
        => throw new InvalidOperationException("The hash has been completed");
}
