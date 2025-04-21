using Nalix.Common.Cryptography.Hashing;
using Nalix.Cryptography.Utilities;
using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace Nalix.Cryptography.Hashing;

/// <summary>
/// Provides an implementation of the SHA-224 cryptographic hash algorithm.
/// </summary>
/// <remarks>
/// SHA-224 is a cryptographic hash function that produces a 224-bit (28-byte) hash value.
/// It is essentially SHA-256 with different initialization values and truncated output.
/// This implementation processes data in 512-bit (64-byte) blocks.
/// </remarks>
public sealed class SHA224 : ISHA, IDisposable
{
    #region Fields

    private readonly uint[] _state = new uint[8];    // Hash state
    private readonly byte[] _buffer = new byte[64];  // Input buffer (64 bytes = 512 bits)
    private readonly uint[] _w = new uint[64];       // Message schedule

    private ulong _totalLength;                      // Total bytes processed
    private int _bufferOffset;                       // Current position in buffer
    private bool _isFinalized;                       // Whether hash has been finalized
    private bool _disposed;                          // Whether instance has been disposed
    private byte[] _finalHash;                       // Final hash value (28 bytes for SHA-224)

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="SHA224"/> class.
    /// </summary>
    public SHA224()
    {
        Initialize();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Resets the hash state to its initial values.
    /// </summary>
    public void Initialize()
    {
        // Initialize state with SHA-224 specific values
        Buffer.BlockCopy(SHA.H224, 0, _state, 0, SHA.H224.Length * sizeof(uint));

        _totalLength = 0;
        _bufferOffset = 0;
        _isFinalized = false;
        _disposed = false;
        _finalHash = null;

        Array.Clear(_buffer, 0, _buffer.Length);
        Array.Clear(_w, 0, _w.Length);
    }

    /// <summary>
    /// Updates the hash with more data.
    /// </summary>
    /// <param name="data">The input data to hash.</param>
    /// <exception cref="ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown if hash has already been finalized.</exception>
    public void Update(ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(condition: _disposed, instance: this);

        if (_isFinalized)
            throw new InvalidOperationException("Hash has already been finalized.");

        if (data.IsEmpty)
            return;

        _totalLength += (ulong)data.Length;

        // Process data in chunks
        int bytesRemaining = data.Length;
        int dataOffset = 0;

        // If we have data in the buffer, try to fill it first
        if (_bufferOffset > 0)
        {
            int bytesToCopy = Math.Min(64 - _bufferOffset, bytesRemaining);
            data.Slice(dataOffset, bytesToCopy).CopyTo(_buffer.AsSpan(_bufferOffset));

            _bufferOffset += bytesToCopy;
            dataOffset += bytesToCopy;
            bytesRemaining -= bytesToCopy;

            // If buffer is full, process it
            if (_bufferOffset == 64)
            {
                ProcessBlock(_buffer);
                _bufferOffset = 0;
            }
        }

        // Process full blocks directly from input
        while (bytesRemaining >= 64)
        {
            // If data is properly aligned, process directly, otherwise copy to buffer
            if (dataOffset % 4 == 0)
            {
                ProcessBlock(data.Slice(dataOffset, 64));
            }
            else
            {
                data.Slice(dataOffset, 64).CopyTo(_buffer);
                ProcessBlock(_buffer);
            }

            dataOffset += 64;
            bytesRemaining -= 64;
        }

        // Store remaining bytes in buffer
        if (bytesRemaining > 0)
        {
            data.Slice(dataOffset, bytesRemaining).CopyTo(_buffer.AsSpan(_bufferOffset));
            _bufferOffset += bytesRemaining;
        }
    }

    /// <summary>
    /// Finalizes the hash computation and returns the hash value.
    /// </summary>
    /// <returns>A 28-byte array containing the SHA-224 hash.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    public byte[] FinalizeHash()
    {
        ObjectDisposedException.ThrowIf(condition: _disposed, instance: this);

        if (!_isFinalized)
        {
            // Padding (same as SHA-256)
            // 1. Append a 1 bit (0x80 byte)
            // 2. Append 0 bits until data is 56 bytes (mod 64)
            // 3. Append bit length (original message length * 8) as 8 bytes

            byte[] padding = new byte[64];
            padding[0] = 0x80; // Append 1 bit

            // Calculate padding length
            ulong bits = _totalLength * 8;
            int padLength = ((_bufferOffset < 56) ? 56 - _bufferOffset : 120 - _bufferOffset);

            // Create a new buffer for padding and bit length
            Span<byte> finalBlock = new byte[padLength + 8];
            finalBlock[0] = 0x80; // Leading 1 bit

            // Add message length as big-endian 64-bit integer
            BinaryPrimitives.WriteUInt64BigEndian(finalBlock[padLength..], bits);

            // Process the final block(s)
            Update(finalBlock[..(padLength + 8)]);

            // Store the final hash (Only store the first 28 bytes for SHA-224)
            _finalHash = new byte[28]; // SHA-224 is 224 bits = 28 bytes

            // Convert state to bytes (big-endian)
            for (int i = 0; i < 7; i++) // Only 7 integers for SHA-224 (224/32 = 7)
            {
                BinaryPrimitives.WriteUInt32BigEndian(_finalHash.AsSpan(i * 4), _state[i]);
            }

            _isFinalized = true;
        }

        return (byte[])_finalHash.Clone();
    }

    /// <summary>
    /// Computes the SHA-224 hash of the given data using an instance method.
    /// </summary>
    /// <param name="data">The input data to hash.</param>
    /// <returns>The computed 224-bit hash as a byte array.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    /// <remarks>
    /// This method allows incremental hashing by calling <see cref="Update"/> before finalizing with <see cref="FinalizeHash"/>.
    /// </remarks>
    public byte[] ComputeHash(ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(SHA224));
        Update(data);
        return FinalizeHash();
    }

    /// <summary>
    /// Computes the SHA-224 hash of the given data in a single call.
    /// </summary>
    /// <param name="data">The input data to hash.</param>
    /// <returns>The computed 224-bit hash as a byte array.</returns>
    public static byte[] HashData(ReadOnlySpan<byte> data)
    {
        using SHA224 sha224 = new();
        sha224.Update(data);
        return sha224.FinalizeHash();
    }

    #endregion

    #region SHA-224/256 Functions

    /// <summary>
    /// Processes a 64-byte block of data.
    /// </summary>
    /// <param name="block">The 64-byte block to process.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProcessBlock(ReadOnlySpan<byte> block)
    {
        if (block.Length != 64)
            throw new ArgumentException("Block size must be 64 bytes", nameof(block));

        // Prepare message schedule (W)
        for (int t = 0; t < 16; t++)
        {
            _w[t] = BinaryPrimitives.ReadUInt32BigEndian(block[(t * 4)..]);
        }

        for (int t = 16; t < 64; t++)
        {
            _w[t] = Sigma1(_w[t - 2]) + _w[t - 7] + Sigma0(_w[t - 15]) + _w[t - 16];
        }

        // Initialize working variables
        uint a = _state[0];
        uint b = _state[1];
        uint c = _state[2];
        uint d = _state[3];
        uint e = _state[4];
        uint f = _state[5];
        uint g = _state[6];
        uint h = _state[7];

        // Main loop
        for (int t = 0; t < 64; t++)
        {
            uint t1 = h + BigSigma1(e) + Ch(e, f, g) + SHA.K224[t] + _w[t];
            uint t2 = BigSigma0(a) + Maj(a, b, c);

            h = g;
            g = f;
            f = e;
            e = d + t1;
            d = c;
            c = b;
            b = a;
            a = t1 + t2;
        }

        // Update state
        _state[0] += a;
        _state[1] += b;
        _state[2] += c;
        _state[3] += d;
        _state[4] += e;
        _state[5] += f;
        _state[6] += g;
        _state[7] += h;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Ch(uint x, uint y, uint z)
        => (x & y) ^ (~x & z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Maj(uint x, uint y, uint z)
        => (x & y) ^ (x & z) ^ (y & z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint BigSigma0(uint x) =>
        BitwiseUtils.RotateRight(x, 2) ^
        BitwiseUtils.RotateRight(x, 13) ^
        BitwiseUtils.RotateRight(x, 22);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint BigSigma1(uint x) =>
        BitwiseUtils.RotateRight(x, 6) ^
        BitwiseUtils.RotateRight(x, 11) ^
        BitwiseUtils.RotateRight(x, 25);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Sigma0(uint x) =>
        BitwiseUtils.RotateRight(x, 7) ^
        BitwiseUtils.RotateRight(x, 18) ^ (x >> 3);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Sigma1(uint x) =>
        BitwiseUtils.RotateRight(x, 17) ^
        BitwiseUtils.RotateRight(x, 19) ^ (x >> 10);

    #endregion

    #region IDisposable

    /// <summary>
    /// Releases resources used by the SHA224 instance.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        // Clear sensitive data
        Array.Clear(_state, 0, _state.Length);
        Array.Clear(_buffer, 0, _buffer.Length);
        Array.Clear(_w, 0, _w.Length);

        if (_finalHash != null)
        {
            Array.Clear(_finalHash, 0, _finalHash.Length);
            _finalHash = null;
        }

        _disposed = true;
    }

    #endregion

    #region Overrides

    /// <summary>
    /// Returns a string representation of the SHA-224 hash algorithm.
    /// </summary>
    public override string ToString() => "SHA-224";

    #endregion
}
