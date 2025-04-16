using Notio.Common.Cryptography.Hashing;
using Notio.Cryptography.Utilities;
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
public sealed class Sha1 : ISha, IDisposable
{
    #region Fields

    // Hash state instance field
    private readonly uint[] _state = new uint[5];
    private bool _disposed;

    // Fields for incremental hashing
    private readonly byte[] _buffer = new byte[64]; // Buffer for incomplete blocks
    private int _bufferIndex = 0;                  // Current position in buffer
    private ulong _totalBytesProcessed = 0;        // Total bytes processed
    private bool _finalized = false;               // Flag indicating hash has been finalized

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the SHA-1 hash algorithm.
    /// </summary>
    public Sha1()
    {
        _disposed = false;
        this.Initialize();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Resets the hash state to initial values.
    /// </summary>
    public void Initialize()
    {
        Buffer.BlockCopy(Sha.H1, 0, _state, 0, Sha.H1.Length * sizeof(uint));
        _bufferIndex = 0;
        _totalBytesProcessed = 0;
        _finalized = false;
    }

    /// <summary>
    /// Updates the hash with more data.
    /// </summary>
    /// <param name="data">The input data to process.</param>
    /// <exception cref="ObjectDisposedException">
    /// Thrown if this method is called after the object has been disposed.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown if this method is called after the hash has been finalized.
    /// </exception>
    public void Update(ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(Sha1));

        if (_finalized)
            throw new InvalidOperationException("Hash has been finalized");

        _totalBytesProcessed += (ulong)data.Length;

        // Process any bytes still in the buffer
        if (_bufferIndex > 0)
        {
            int bytesToCopy = Math.Min(64 - _bufferIndex, data.Length);
            data[..bytesToCopy].CopyTo(_buffer.AsSpan()[_bufferIndex..]);
            _bufferIndex += bytesToCopy;

            if (_bufferIndex == 64)
            {
                ProcessBlock(_buffer, _state);
                _bufferIndex = 0;
            }

            data = data[bytesToCopy..];
        }

        // Process full blocks directly from input
        while (data.Length >= 64)
        {
            ProcessBlock(data[..64], _state);
            data = data[64..];
        }

        // Store remaining bytes in buffer
        if (data.Length > 0)
        {
            data.CopyTo(_buffer.AsSpan()[_bufferIndex..]);
            _bufferIndex += data.Length;
        }
    }

    /// <summary>
    /// Finalizes the hash computation and returns the hash value.
    /// </summary>
    /// <returns>The computed hash.</returns>
    /// <exception cref="ObjectDisposedException">
    /// Thrown if this method is called after the object has been disposed.
    /// </exception>
    public byte[] FinalizeHash()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(Sha1));

        byte[] result = new byte[20];

        if (_finalized)
        {
            // Create a copy of the hash result without reprocessing
            for (int i = 0; i < 5; i++)
            {
                BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan()[(i * 4)..], _state[i]);
            }
            return result;
        }

        // Calculate message length in bits
        ulong bitLength = _totalBytesProcessed * 8;

        // Add padding as in ComputeHash method
        Span<byte> paddingBuffer = stackalloc byte[128]; // Max 2 blocks needed
        int paddingBufferPos = 0;

        // Copy remaining data from buffer
        if (_bufferIndex > 0)
        {
            _buffer.AsSpan(0, _bufferIndex).CopyTo(paddingBuffer);
            paddingBufferPos = _bufferIndex;
        }

        // Add the '1' bit
        paddingBuffer[paddingBufferPos++] = 0x80;

        // Determine if we need one or two blocks
        int blockCount = (paddingBufferPos + 8 > 64) ? 2 : 1;
        int finalBlockSize = blockCount * 64;

        // Fill with zeros until length field
        while (paddingBufferPos < finalBlockSize - 8)
        {
            paddingBuffer[paddingBufferPos++] = 0;
        }

        // Write the length in bits as a 64-bit big-endian integer
        BinaryPrimitives.WriteUInt64BigEndian(
            paddingBuffer[(finalBlockSize - 8)..],
            bitLength);

        // Process the final block(s)
        for (int i = 0; i < blockCount; i++)
            ProcessBlock(paddingBuffer.Slice(i * 64, 64), _state);

        _finalized = true;

        // Convert hash to bytes in big-endian format
        for (int i = 0; i < 5; i++)
            BinaryPrimitives.WriteUInt32BigEndian(
                result.AsSpan()[(i * 4)..], _state[i]);

        return result;
    }

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
    /// <exception cref="ObjectDisposedException">
    /// Thrown if this method is called after the object has been disposed.
    /// </exception>
    /// <remarks>
    /// This method follows the standard SHA-1 padding and processing rules:
    /// - The input data is processed in 64-byte blocks.
    /// - A padding byte `0x80` is added after the data.
    /// - The length of the original message (in bits) is appended in big-endian format.
    /// </remarks>
    public byte[] ComputeHash(ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(Sha1));

        // Reset the state to ensure independence from previous operations
        Initialize();

        // Create a temporary copy of the hash state to preserve the instance state
        Span<uint> h = stackalloc uint[5];
        _state.CopyTo(h);

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

    /// <summary>
    /// Computes the SHA-1 hash of the given data in a single call.
    /// </summary>
    /// <param name="data">The input data to hash.</param>
    /// <returns>The computed 160-bit hash as a byte array.</returns>
    /// <remarks>
    /// This method is a convenience wrapper that initializes, updates, and finalizes the hash computation.
    /// </remarks>
    public static byte[] HashData(ReadOnlySpan<byte> data)
    {
        using Sha1 sha1 = new();
        sha1.Update(data);
        return sha1.FinalizeHash();
    }

    #endregion

    #region Private Methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProcessBlock(ReadOnlySpan<byte> block, Span<uint> h)
    {
        // Use hardware acceleration if available
        if (Ssse3.IsSupported)
        {
            ProcessBlockSsse3(block, h);
            return;
        }

        ProcessBlockScalar(block, h);
    }

    // Rest of the implementation remains the same...
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void ProcessBlockScalar(ReadOnlySpan<byte> block, Span<uint> h)
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
            w[j] = BitwiseUtils.RotateLeft(w[j - 3] ^ w[j - 8] ^ w[j - 14] ^ w[j - 16], 1);
        }

        // Initialize working variables
        uint a = h[0], b = h[1], c = h[2], d = h[3], e = h[4];

        // Main loop - optimized with function inlining
        for (int j = 0; j < 20; j++)
        {
            uint temp = BitwiseUtils.RotateLeft(a, 5) + ((b & c) | ((~b) & d)) + e + Sha.K1[0] + w[j];
            e = d;
            d = c;
            c = BitwiseUtils.RotateLeft(b, 30);
            b = a;
            a = temp;
        }

        for (int j = 20; j < 40; j++)
        {
            uint temp = BitwiseUtils.RotateLeft(a, 5) + (b ^ c ^ d) + e + Sha.K1[1] + w[j];
            e = d;
            d = c;
            c = BitwiseUtils.RotateLeft(b, 30);
            b = a;
            a = temp;
        }

        for (int j = 40; j < 60; j++)
        {
            uint temp = BitwiseUtils.RotateLeft(a, 5) + ((b & c) | (b & d) | (c & d)) + e + Sha.K1[2] + w[j];
            e = d;
            d = c;
            c = BitwiseUtils.RotateLeft(b, 30);
            b = a;
            a = temp;
        }

        for (int j = 60; j < 80; j++)
        {
            uint temp = BitwiseUtils.RotateLeft(a, 5) + (b ^ c ^ d) + e + Sha.K1[3] + w[j];
            e = d;
            d = c;
            c = BitwiseUtils.RotateLeft(b, 30);
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
    private unsafe void ProcessBlockSsse3(ReadOnlySpan<byte> block, Span<uint> h)
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
                w[i + j] = BitwiseUtils.RotateLeft(
                    w[(i + j) - 3] ^ w[(i + j) - 8] ^ w[(i + j) - 14] ^ w[(i + j) - 16], 1);
            }
        }

        // Initialize working variables
        uint a = h[0], b = h[1], c = h[2], d = h[3], e = h[4];

        // Process rounds with loop unrolling
        for (int j = 0; j < 20; j += 5)
        {
            ProcessRound(ref a, ref b, ref c, ref d, ref e, CH(b, c, d), Sha.K1[0], w[j]);
            ProcessRound(ref e, ref a, ref b, ref c, ref d, CH(a, b, c), Sha.K1[0], w[j + 1]);
            ProcessRound(ref d, ref e, ref a, ref b, ref c, CH(e, a, b), Sha.K1[0], w[j + 2]);
            ProcessRound(ref c, ref d, ref e, ref a, ref b, CH(d, e, a), Sha.K1[0], w[j + 3]);
            ProcessRound(ref b, ref c, ref d, ref e, ref a, CH(c, d, e), Sha.K1[0], w[j + 4]);
        }

        for (int j = 20; j < 40; j += 5)
        {
            ProcessRound(ref a, ref b, ref c, ref d, ref e, PARITY(b, c, d), Sha.K1[1], w[j]);
            ProcessRound(ref e, ref a, ref b, ref c, ref d, PARITY(a, b, c), Sha.K1[1], w[j + 1]);
            ProcessRound(ref d, ref e, ref a, ref b, ref c, PARITY(e, a, b), Sha.K1[1], w[j + 2]);
            ProcessRound(ref c, ref d, ref e, ref a, ref b, PARITY(d, e, a), Sha.K1[1], w[j + 3]);
            ProcessRound(ref b, ref c, ref d, ref e, ref a, PARITY(c, d, e), Sha.K1[1], w[j + 4]);
        }

        for (int j = 40; j < 60; j += 5)
        {
            ProcessRound(ref a, ref b, ref c, ref d, ref e, MAJ(b, c, d), Sha.K1[2], w[j]);
            ProcessRound(ref e, ref a, ref b, ref c, ref d, MAJ(a, b, c), Sha.K1[2], w[j + 1]);
            ProcessRound(ref d, ref e, ref a, ref b, ref c, MAJ(e, a, b), Sha.K1[2], w[j + 2]);
            ProcessRound(ref c, ref d, ref e, ref a, ref b, MAJ(d, e, a), Sha.K1[2], w[j + 3]);
            ProcessRound(ref b, ref c, ref d, ref e, ref a, MAJ(c, d, e), Sha.K1[2], w[j + 4]);
        }

        for (int j = 60; j < 80; j += 5)
        {
            ProcessRound(ref a, ref b, ref c, ref d, ref e, PARITY(b, c, d), Sha.K1[3], w[j]);
            ProcessRound(ref e, ref a, ref b, ref c, ref d, PARITY(a, b, c), Sha.K1[3], w[j + 1]);
            ProcessRound(ref d, ref e, ref a, ref b, ref c, PARITY(e, a, b), Sha.K1[3], w[j + 2]);
            ProcessRound(ref c, ref d, ref e, ref a, ref b, PARITY(d, e, a), Sha.K1[3], w[j + 3]);
            ProcessRound(ref b, ref c, ref d, ref e, ref a, PARITY(c, d, e), Sha.K1[3], w[j + 4]);
        }

        // Update hash state
        h[0] += a;
        h[1] += b;
        h[2] += c;
        h[3] += d;
        h[4] += e;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ProcessRound(
        ref uint a, ref uint b, ref uint c, ref uint d, ref uint e, uint f, uint k, uint w)
    {
        uint temp = BitwiseUtils.RotateLeft(a, 5) + f + e + k + w;
        e = d;
        d = c;
        c = BitwiseUtils.RotateLeft(b, 30);
        b = a;
        a = temp;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint CH(uint x, uint y, uint z) => (x & y) ^ ((~x) & z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint PARITY(uint x, uint y, uint z) => x ^ y ^ z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint MAJ(uint x, uint y, uint z) => (x & y) ^ (x & z) ^ (y & z);

    #endregion

    #region IDisposable Implementation

    /// <summary>
    /// Releases all resources used by the <see cref="Sha1"/> instance.
    /// </summary>
    /// <remarks>
    /// This method clears sensitive data from memory and marks the instance as disposed.
    /// </remarks>
    public void Dispose()
    {
        if (_disposed) return;

        // Clear sensitive data from memory
        Array.Clear(_state, 0, _state.Length);
        Array.Clear(_buffer, 0, _buffer.Length);

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    #endregion

    #region Overrides

    /// <summary>
    /// Returns a string representation of the SHA-1 hash algorithm.
    /// </summary>
    public override string ToString() => "SHA-1";

    #endregion
}
