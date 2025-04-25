using Nalix.Common.Cryptography.Hashing;
using Nalix.Cryptography.Utilities;
using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Nalix.Cryptography.Hashing;

/// <summary>
/// Provides an optimized implementation of the SHA-1 cryptographic hash algorithm.
/// </summary>
/// <remarks>
/// SHA-1 is a cryptographic hash function that produces a 160-bit (20-byte) hash value.
/// It is considered weak due to known vulnerabilities but is still used in legacy systems.
/// This implementation processes data in 512-bit (64-byte) blocks.
/// </remarks>
public sealed class SHA1 : ISHA, IDisposable
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
    public static byte[] HashData(ReadOnlySpan<byte> data)
    {
        using SHA1 sha1 = new();
        sha1.Update(data);
        return sha1.FinalizeHash();
    }

    /// <summary>
    /// Resets the hash state to initial values.
    /// </summary>
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
    public byte[] FinalizeHash()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(SHA1));

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
        ObjectDisposedException.ThrowIf(_disposed, nameof(SHA1));

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
            this.ProcessBlock(data.Slice(i * 64, 64), h);

        // Handle the final block with padding
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

        // Write the length in bits as a 64-bit big-endian integer
        BinaryPrimitives.WriteUInt64BigEndian(
            finalBlock[(finalBlockSize - 8)..],
            bitLength);

        // Process the final block(s)
        for (int i = 0; i < blockCount; i++)
            this.ProcessBlock(finalBlock.Slice(i * 64, 64), h);

        // Convert the hash to bytes in big-endian format
        Span<byte> result = stackalloc byte[20];
        for (int i = 0; i < 5; i++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(result[(i * 4)..], h[i]);
            _state[i] = h[i];
        }

        return result.ToArray();
    }

    #endregion Public Methods

    #region Private Methods

    // Rest of the implementation remains the same...
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void ProcessBlock(ReadOnlySpan<byte> block, Span<uint> h)
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
