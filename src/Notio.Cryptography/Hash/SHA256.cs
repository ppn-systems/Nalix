using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Notio.Cryptography.Hash;

/// <summary>
/// Provides an optimized implementation of the SHA-256 cryptographic hash algorithm.
/// </summary>
public sealed class SHA256 : IDisposable
{
    private readonly byte[] _buffer = new byte[64];
    private readonly uint[] _state = new uint[8];
    private byte[] _finalHash;
    private int _bufferLength;
    private ulong _byteCount;
    private bool _finalized;

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

        int dataOffset = 0;
        int dataLength = data.Length;

        // Process any previously buffered data.
        if (_bufferLength > 0)
        {
            int remainingBufferSpace = 64 - _bufferLength;
            int bytesToCopy = Math.Min(remainingBufferSpace, dataLength);

            data[..bytesToCopy].CopyTo(_buffer.AsSpan(_bufferLength));
            _bufferLength += bytesToCopy;
            dataOffset += bytesToCopy;
            dataLength -= bytesToCopy;

            if (_bufferLength == 64)
            {
                ProcessBlock(_buffer);
                _bufferLength = 0;
            }
        }

        // Process full blocks from the input.
        while (dataLength >= 64)
        {
            ProcessBlock(data.Slice(dataOffset, 64));
            dataOffset += 64;
            dataLength -= 64;
        }

        // Buffer any remaining data.
        if (dataLength > 0)
        {
            data.Slice(dataOffset, dataLength).CopyTo(_buffer.AsSpan(_bufferLength));
            _bufferLength += dataLength;
        }

        _byteCount += (ulong)data.Length;
    }

    /// <summary>
    /// Finalizes the hash computation and returns the result.
    /// </summary>
    /// <returns>The final hash value.</returns>
    public byte[] FinalizeHash()
    {
        if (_finalized) return (byte[])_finalHash.Clone();

        // Compute padding.
        int padLength = (_byteCount % 64 < 56) ? (56 - (int)(_byteCount % 64)) : (120 - (int)(_byteCount % 64));

        Span<byte> padding = stackalloc byte[64];
        padding[0] = 0x80; // append the '1' bit
        padding[1..padLength].Clear();

        // Append the 64-bit big-endian message length.
        Span<byte> lengthBytes = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(lengthBytes, _byteCount * 8);

        // Update with padding and length.
        Update(padding[..padLength]);
        Update(lengthBytes);

        _finalized = true;
        byte[] finalHash = new byte[32];

        // Write the state to the output in big-endian order.
        for (int i = 0; i < 8; i++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(finalHash.AsSpan(i * 4), _state[i]);
        }

        _finalHash = finalHash;
        return finalHash;
    }

    /// <summary>
    /// Gets the computed hash value after finalization.
    /// Throws an exception if the hash has not been finalized.
    /// </summary>
    public byte[] Hash
    {
        get
        {
            if (!_finalized)
                throw new InvalidOperationException(
                    "The hash has not been completed. Call TransformFinalBlock before accessing the Hash.");
            return (byte[])_finalHash.Clone();
        }
    }

    /// <summary>
    /// Processes a block of 64 bytes.
    /// This method chooses an intrinsic–based implementation when available.
    /// </summary>
    /// <param name="block">A 64-byte block to process.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProcessBlock(ReadOnlySpan<byte> block)
    {
        if (Avx2.IsSupported)
        {
            ProcessBlockAvx2(block);
        }
        else if (Avx.IsSupported)
        {
            ProcessBlockAvx(block);
        }
        else if (Ssse3.IsSupported)
        {
            ProcessBlockIntrinsic(block);
        }
        else
        {
            ProcessBlockScalar(block);
        }
    }

    /// <summary>
    /// Scalar (fallback) implementation of block processing.
    /// </summary>
    /// <param name="block">A 64-byte block.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void ProcessBlockScalar(ReadOnlySpan<byte> block)
    {
        if (block.Length < 64)
            throw new ArgumentException("Invalid block size.");

        // Allocate W[64] on the stack.
        uint* W = stackalloc uint[64];
        uint a = _state[0], b = _state[1], c = _state[2], d = _state[3];
        uint e = _state[4], f = _state[5], g = _state[6], h = _state[7];

        // Load the first 16 words (big-endian)
        for (int i = 0; i < 16; i++)
        {
            W[i] = BinaryPrimitives.ReadUInt32BigEndian(block[(i * 4)..]);
        }

        // Message schedule expansion.
        for (int i = 16; i < 64; i++)
        {
            uint s0 = RotateRight(W[i - 15], 7) ^ RotateRight(W[i - 15], 18) ^ (W[i - 15] >> 3);
            uint s1 = RotateRight(W[i - 2], 17) ^ RotateRight(W[i - 2], 19) ^ (W[i - 2] >> 10);
            W[i] = W[i - 16] + s0 + W[i - 7] + s1;
        }

        // Process rounds.
        for (int i = 0; i < 64; i += 8)
        {
            Round(ref a, ref b, ref c, ref d, ref e, ref f, ref g, ref h, W[i], K[i]);
            Round(ref h, ref a, ref b, ref c, ref d, ref e, ref f, ref g, W[i + 1], K[i + 1]);
            Round(ref g, ref h, ref a, ref b, ref c, ref d, ref e, ref f, W[i + 2], K[i + 2]);
            Round(ref f, ref g, ref h, ref a, ref b, ref c, ref d, ref e, W[i + 3], K[i + 3]);
            Round(ref e, ref f, ref g, ref h, ref a, ref b, ref c, ref d, W[i + 4], K[i + 4]);
            Round(ref d, ref e, ref f, ref g, ref h, ref a, ref b, ref c, W[i + 5], K[i + 5]);
            Round(ref c, ref d, ref e, ref f, ref g, ref h, ref a, ref b, W[i + 6], K[i + 6]);
            Round(ref b, ref c, ref d, ref e, ref f, ref g, ref h, ref a, W[i + 7], K[i + 7]);
        }

        _state[0] += a; _state[1] += b; _state[2] += c; _state[3] += d;
        _state[4] += e; _state[5] += f; _state[6] += g; _state[7] += h;
    }

    /// <summary>
    /// Intrinsics–based implementation using SSSE3 for fast byte–swapping.
    /// </summary>
    /// <param name="block">A 64-byte block.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void ProcessBlockIntrinsic(ReadOnlySpan<byte> block)
    {
        if (block.Length < 64)
            throw new ArgumentException("Invalid block size.");

        // Allocate W[64] on the stack.
        uint* W = stackalloc uint[64];
        uint a = _state[0], b = _state[1], c = _state[2], d = _state[3];
        uint e = _state[4], f = _state[5], g = _state[6], h = _state[7];

        // Load the first 16 words (big-endian) using SIMD for faster loading and byte-swapping.
        fixed (byte* p = block)
        {
            // Shuffle mask to convert big-endian to little-endian.
            Vector128<byte> shuffleMask = Vector128.Create(
                (byte)3, (byte)2, (byte)1, (byte)0,
                (byte)7, (byte)6, (byte)5, (byte)4,
                (byte)11, (byte)10, (byte)9, (byte)8,
                (byte)15, (byte)14, (byte)13, (byte)12
            );

            // Load 16 bytes at a time and shuffle.
            Vector128<uint> v0 = Ssse3.Shuffle(Sse2.LoadVector128(p), shuffleMask).AsUInt32();
            Vector128<uint> v1 = Ssse3.Shuffle(Sse2.LoadVector128(p + 16), shuffleMask).AsUInt32();
            Vector128<uint> v2 = Ssse3.Shuffle(Sse2.LoadVector128(p + 32), shuffleMask).AsUInt32();
            Vector128<uint> v3 = Ssse3.Shuffle(Sse2.LoadVector128(p + 48), shuffleMask).AsUInt32();

            // Store the shuffled words into W.
            Sse2.Store(&W[0], v0);
            Sse2.Store(&W[4], v1);
            Sse2.Store(&W[8], v2);
            Sse2.Store(&W[12], v3);
        }

        // Message schedule expansion (scalar).
        for (int i = 16; i < 64; i++)
        {
            uint s0 = RotateRight(W[i - 15], 7) ^ RotateRight(W[i - 15], 18) ^ (W[i - 15] >> 3);
            uint s1 = RotateRight(W[i - 2], 17) ^ RotateRight(W[i - 2], 19) ^ (W[i - 2] >> 10);
            W[i] = W[i - 16] + s0 + W[i - 7] + s1;
        }

        // Process rounds.
        for (int i = 0; i < 64; i += 8)
        {
            Round(ref a, ref b, ref c, ref d, ref e, ref f, ref g, ref h, W[i], K[i]);
            Round(ref h, ref a, ref b, ref c, ref d, ref e, ref f, ref g, W[i + 1], K[i + 1]);
            Round(ref g, ref h, ref a, ref b, ref c, ref d, ref e, ref f, W[i + 2], K[i + 2]);
            Round(ref f, ref g, ref h, ref a, ref b, ref c, ref d, ref e, W[i + 3], K[i + 3]);
            Round(ref e, ref f, ref g, ref h, ref a, ref b, ref c, ref d, W[i + 4], K[i + 4]);
            Round(ref d, ref e, ref f, ref g, ref h, ref a, ref b, ref c, W[i + 5], K[i + 5]);
            Round(ref c, ref d, ref e, ref f, ref g, ref h, ref a, ref b, W[i + 6], K[i + 6]);
            Round(ref b, ref c, ref d, ref e, ref f, ref g, ref h, ref a, W[i + 7], K[i + 7]);
        }

        _state[0] += a; _state[1] += b; _state[2] += c; _state[3] += d;
        _state[4] += e; _state[5] += f; _state[6] += g; _state[7] += h;
    }

    /// <summary>
    /// Optimized block processing using AVX2 intrinsics.
    /// This implementation uses 256–bit loads and shuffles for the initial 32 bytes
    /// of the block. (Message schedule expansion and rounds remain scalar for simplicity.)
    /// </summary>
    /// <param name="block">A 64-byte block.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void ProcessBlockAvx2(ReadOnlySpan<byte> block)
    {
        if (block.Length < 64)
            throw new ArgumentException("Invalid block size.");

        uint* W = stackalloc uint[64];
        uint a = _state[0], b = _state[1], c = _state[2], d = _state[3];
        uint e = _state[4], f = _state[5], g = _state[6], h = _state[7];

        fixed (byte* p = block)
        {
            // Load 32 bytes at a time.
            Vector256<byte> v0 = Avx.LoadVector256(p);
            // Create a 256–bit shuffle mask for byte–swapping.
            Vector256<byte> shuffleMask = Vector256.Create(
                (byte)3, (byte)2, (byte)1, (byte)0,
                (byte)7, (byte)6, (byte)5, (byte)4,
                (byte)11, (byte)10, (byte)9, (byte)8,
                (byte)15, (byte)14, (byte)13, (byte)12,
                (byte)3, (byte)2, (byte)1, (byte)0,
                (byte)7, (byte)6, (byte)5, (byte)4,
                (byte)11, (byte)10, (byte)9, (byte)8,
                (byte)15, (byte)14, (byte)13, (byte)12
            );
            Vector256<byte> swapped0 = Avx2.Shuffle(v0, shuffleMask);

            // Load the next 32 bytes.
            Vector256<byte> v1 = Avx.LoadVector256(p + 32);
            Vector256<byte> swapped1 = Avx2.Shuffle(v1, shuffleMask);

            // Store into the message schedule W (first 16 words).
            Avx.Store(W, swapped0.AsUInt32());
            Avx.Store(W + 8, swapped1.AsUInt32());
        }

        // Message schedule expansion (scalar).
        for (int i = 16; i < 64; i++)
        {
            uint s0 = RotateRight(W[i - 15], 7) ^ RotateRight(W[i - 15], 18) ^ (W[i - 15] >> 3);
            uint s1 = RotateRight(W[i - 2], 17) ^ RotateRight(W[i - 2], 19) ^ (W[i - 2] >> 10);
            W[i] = W[i - 16] + s0 + W[i - 7] + s1;
        }

        // Process rounds (scalar).
        for (int i = 0; i < 64; i += 8)
        {
            Round(ref a, ref b, ref c, ref d, ref e, ref f, ref g, ref h, W[i], K[i]);
            Round(ref h, ref a, ref b, ref c, ref d, ref e, ref f, ref g, W[i + 1], K[i + 1]);
            Round(ref g, ref h, ref a, ref b, ref c, ref d, ref e, ref f, W[i + 2], K[i + 2]);
            Round(ref f, ref g, ref h, ref a, ref b, ref c, ref d, ref e, W[i + 3], K[i + 3]);
            Round(ref e, ref f, ref g, ref h, ref a, ref b, ref c, ref d, W[i + 4], K[i + 4]);
            Round(ref d, ref e, ref f, ref g, ref h, ref a, ref b, ref c, W[i + 5], K[i + 5]);
            Round(ref c, ref d, ref e, ref f, ref g, ref h, ref a, ref b, W[i + 6], K[i + 6]);
            Round(ref b, ref c, ref d, ref e, ref f, ref g, ref h, ref a, W[i + 7], K[i + 7]);
        }

        _state[0] += a; _state[1] += b; _state[2] += c; _state[3] += d;
        _state[4] += e; _state[5] += f; _state[6] += g; _state[7] += h;
    }

    /// <summary>
    /// Optimized block processing using AVX intrinsics.
    /// This stub currently falls back to the scalar implementation.
    /// </summary>
    /// <param name="block">A 64-byte block.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProcessBlockAvx(ReadOnlySpan<byte> block)
    {
        // A proper AVX implementation might use 256–bit loads and operations.
        // For now, we fall back to the scalar implementation.
        ProcessBlockScalar(block);
    }

    /// <summary>
    /// Performs one SHA-256 round.
    /// </summary>
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

    /// <summary>
    /// Rotate a 32–bit word right by the specified number of bits.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint RotateRight(uint value, int bits) =>
        (value >> bits) | (value << (32 - bits));

    /// <summary>
    /// Updates the hash state with a block of data (similar to TransformBlock).
    /// </summary>
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
    /// Updates the final block of data and completes the hash computation (similar to TransformFinalBlock).
    /// </summary>
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

    // SHA-256 constants
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
}
