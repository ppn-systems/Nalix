using Nalix.Common.Cryptography.Hashing;
using Nalix.Cryptography.Internal;
using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Nalix.Cryptography.Hashing;

/// <summary>
/// Provides an optimized implementation of the SHA-1 cryptographic hash algorithm.
/// </summary>
/// <remarks>
/// SHA-1 is a cryptographic hash function that produces a 160-bit (20-byte) hash value.
/// It is considered weak due to known vulnerabilities but is still used in legacy systems.
/// This implementation processes data in 512-bit (64-byte) blocks.
/// </remarks>
[System.Runtime.InteropServices.ComVisible(true)]
public sealed class SHA1 : IShaDigest, IDisposable
{
    #region Fields

    // Hash state instance field
    private readonly uint[] _state = new uint[5];

    private bool _disposed = false;

    // Fields for incremental hashing
    private readonly byte[] _buffer = new byte[64]; // Buffer for incomplete blocks

    private int _bufferIndex = 0;                  // Current position in buffer
    private ulong _totalBytesProcessed = 0;        // Total bytes processed
    private bool _finalized = false;               // Flag indicating hash has been finalized

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the SHA-1 hash algorithm.
    /// </summary>
    public SHA1() => this.Initialize();

    #endregion Constructors

    #region Public Methods

    /// <summary>
    /// Computes the SHA-1 hash of the given data in a single call.
    /// </summary>
    /// <param name="data">The input data to hash.</param>
    /// <returns>The computed 160-bit hash as a byte array.</returns>
    /// <remarks>
    /// This method is a convenience wrapper that initializes, updates, and finalizes the hash computation.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] HashData(ReadOnlySpan<byte> data)
    {
        using SHA1 sha1 = new();
        sha1.Update(data);
        return sha1.FinalizeHash();
    }

    /// <summary>
    /// Resets the hash state to initial values.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Initialize()
    {
        Buffer.BlockCopy(SHA.H1, 0, _state, 0, SHA.H1.Length * sizeof(uint));
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update(ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(SHA1));

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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] FinalizeHash()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(SHA1));

        byte[] result = new byte[20];

        if (_finalized)
        {
            // Create a copy of the hash result without reprocessing
            unsafe
            {
                fixed (byte* p = result)
                {
                    uint* ptr = (uint*)p;
                    for (int i = 0; i < 5; i++)
                        ptr[i] = BinaryPrimitives.ReverseEndianness(_state[i]);
                }
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

        // WriteInt16 the length in bits as a 64-bit big-endian integer
        BinaryPrimitives.WriteUInt64BigEndian(
            paddingBuffer[(finalBlockSize - 8)..],
            bitLength);

        // Process the final block(s)
        for (int i = 0; i < blockCount; i++)
            ProcessBlock(paddingBuffer.Slice(i * 64, 64), _state);

        _finalized = true;

        // Convert hash to bytes in big-endian format
        unsafe
        {
            fixed (byte* p = result)
            {
                uint* ptr = (uint*)p;
                for (int i = 0; i < 5; i++)
                    ptr[i] = BinaryPrimitives.ReverseEndianness(_state[i]);
            }
        }

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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] ComputeHash(ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(SHA1));

        // Reset the state to ensure independence from previous operations
        Initialize();

        // Create a temporary copy of the hash state to preserve the instance state
        Span<uint> h = stackalloc uint[5];

        unsafe
        {
            ref byte srcRef = ref Unsafe.As<uint, byte>(ref MemoryMarshal.GetArrayDataReference(_state));
            ref byte dstRef = ref Unsafe.As<uint, byte>(ref MemoryMarshal.GetReference(h));

            Unsafe.CopyBlockUnaligned(ref dstRef, ref srcRef, 20); // 5 * sizeof(uint)
        }

        // Calculate message length in bits (before padding)
        ulong bitLength = (ulong)data.Length * 8;

        // Process all complete blocks
        int fullBlocks = data.Length / 64;
        unsafe
        {
            ref byte inputRef = ref MemoryMarshal.GetReference(data);
            for (int i = 0; i < fullBlocks; i++)
            {
                ReadOnlySpan<byte> block = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref inputRef, i * 64), 64);
                ProcessBlock(block, h);
            }
        }

        // Token the final block with padding
        int remainingBytes = data.Length % 64;
        Span<byte> finalBlock = stackalloc byte[128]; // Max 2 blocks needed

        // Copy remaining data to the final block
        if (remainingBytes > 0)
            data[^remainingBytes..].CopyTo(finalBlock);

        // Add the '1' bit
        finalBlock[remainingBytes] = 0x80;

        // Determine if we need one or two blocks
        int blockCount = (remainingBytes + 1 + 8 > 64) ? 2 : 1;
        int finalBlockSize = blockCount * 64;

        // WriteInt16 the length in bits as a 64-bit big-endian integer
        unsafe
        {
            fixed (byte* p = finalBlock)
            {
                *(ulong*)(p + finalBlockSize - 8) = BitConverter.IsLittleEndian
                    ? BinaryPrimitives.ReverseEndianness(bitLength)
                    : bitLength;
            }
        }

        // Process the final block(s)
        for (int i = 0; i < blockCount; i++)
            ProcessBlock(finalBlock.Slice(i * 64, 64), h);

        // Convert the hash to bytes in big-endian format
        unsafe
        {
            byte[] result = new byte[20];
            fixed (byte* resultPtr = result)
            {
                uint v0 = h[0];
                uint v1 = h[1];
                uint v2 = h[2];
                uint v3 = h[3];
                uint v4 = h[4];

                if (BitConverter.IsLittleEndian)
                {
                    v0 = BinaryPrimitives.ReverseEndianness(v0);
                    v1 = BinaryPrimitives.ReverseEndianness(v1);
                    v2 = BinaryPrimitives.ReverseEndianness(v2);
                    v3 = BinaryPrimitives.ReverseEndianness(v3);
                    v4 = BinaryPrimitives.ReverseEndianness(v4);
                }

                ((uint*)resultPtr)[0] = v0;
                ((uint*)resultPtr)[1] = v1;
                ((uint*)resultPtr)[2] = v2;
                ((uint*)resultPtr)[3] = v3;
                ((uint*)resultPtr)[4] = v4;

                _state[0] = h[0];
                _state[1] = h[1];
                _state[2] = h[2];
                _state[3] = h[3];
                _state[4] = h[4];
            }
            return result;
        }
    }

    #endregion Public Methods

    #region Private Methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void ProcessBlock(ReadOnlySpan<byte> block, Span<uint> h)
    {
        Span<uint> w = stackalloc uint[80];

        // Load first 16 words from big-endian data
        fixed (byte* ptr = block)
        {
            for (int j = 0; j < 16; j++)
                w[j] = BinaryPrimitives.ReverseEndianness(Unsafe.ReadUnaligned<uint>(ptr + (j << 2)));
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
            uint temp = BitwiseUtils.RotateLeft(a, 5) + ((b & c) | ((~b) & d)) + e + SHA.K1[0] + w[j];
            e = d;
            d = c;
            c = BitwiseUtils.RotateLeft(b, 30);
            b = a;
            a = temp;
        }

        for (int j = 20; j < 40; j++)
        {
            uint temp = BitwiseUtils.RotateLeft(a, 5) + (b ^ c ^ d) + e + SHA.K1[1] + w[j];
            e = d;
            d = c;
            c = BitwiseUtils.RotateLeft(b, 30);
            b = a;
            a = temp;
        }

        for (int j = 40; j < 60; j++)
        {
            uint temp = BitwiseUtils.RotateLeft(a, 5) + ((b & c) | (b & d) | (c & d)) + e + SHA.K1[2] + w[j];
            e = d;
            d = c;
            c = BitwiseUtils.RotateLeft(b, 30);
            b = a;
            a = temp;
        }

        for (int j = 60; j < 80; j++)
        {
            uint temp = BitwiseUtils.RotateLeft(a, 5) + (b ^ c ^ d) + e + SHA.K1[3] + w[j];
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

    #endregion Private Methods

    #region IDisposable Implementation

    /// <summary>
    /// Releases all resources used by the <see cref="SHA1"/> instance.
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

    #endregion IDisposable Implementation

    #region Overrides

    /// <summary>
    /// Returns a string representation of the SHA-1 hash algorithm.
    /// </summary>
    public override string ToString() => "SHA-1";

    #endregion Overrides
}