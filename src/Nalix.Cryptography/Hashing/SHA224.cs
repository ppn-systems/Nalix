using Nalix.Common.Security.Cryptography.Hashing;
using Nalix.Cryptography.Internal;
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
[System.Runtime.InteropServices.ComVisible(true)]
public sealed class SHA224 : IShaDigest, IDisposable
{
    #region Fields

    private readonly UInt32[] _state = new UInt32[8];    // Hash state
    private readonly Byte[] _buffer = new Byte[64];  // Input buffer (64 bytes = 512 bits)
    private readonly UInt32[] _w = new UInt32[64];       // Message schedule

    private UInt64 _totalLength;                      // Total bytes processed
    private Int32 _bufferOffset;                       // Current position in buffer
    private Boolean _isFinalized;                       // Whether hash has been finalized
    private Boolean _disposed;                          // Whether instance has been disposed
    private Byte[] _finalHash;                       // Final hash value (28 bytes for SHA-224)

    #endregion Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="SHA224"/> class.
    /// </summary>
    public SHA224() => Initialize();

    #endregion Constructor

    #region Public Methods

    /// <summary>
    /// Computes the SHA-224 hash of the given data in a single call.
    /// </summary>
    /// <param name="data">The input data to hash.</param>
    /// <returns>The computed 224-bit hash as a byte array.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Byte[] HashData(ReadOnlySpan<Byte> data)
    {
        using SHA224 sha224 = new();
        sha224.Update(data);
        return sha224.FinalizeHash();
    }

    /// <summary>
    /// Resets the hash state to its initial values.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Initialize()
    {
        // Initialize state with SHA-224 specific values
        Buffer.BlockCopy(SHA.H224, 0, _state, 0, SHA.H224.Length * sizeof(UInt32));

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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update(ReadOnlySpan<Byte> data)
    {
        ObjectDisposedException.ThrowIf(condition: _disposed, instance: this);

        if (_isFinalized)
        {
            throw new InvalidOperationException("Hash has already been finalized.");
        }

        if (data.IsEmpty)
        {
            return;
        }

        _totalLength += (UInt64)data.Length;

        // Process data in chunks
        Int32 bytesRemaining = data.Length;
        Int32 dataOffset = 0;

        // If we have data in the buffer, try to fill it first
        if (_bufferOffset > 0)
        {
            Int32 bytesToCopy = Math.Min(64 - _bufferOffset, bytesRemaining);
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Byte[] FinalizeHash()
    {
        ObjectDisposedException.ThrowIf(condition: _disposed, instance: this);

        if (!_isFinalized)
        {
            // Padding (same as SHA-256)
            // 1. Append a 1 bit (0x80 byte)
            // 2. Append 0 bits until data is 56 bytes (mod 64)
            // 3. Append bit length (original message length * 8) as 8 bytes

            Byte[] padding = new Byte[64];
            padding[0] = 0x80; // Append 1 bit

            // Calculate padding length
            UInt64 bits = _totalLength * 8;
            Int32 padLength = (_bufferOffset < 56) ? 56 - _bufferOffset : 120 - _bufferOffset;

            // Create a new buffer for padding and bit length
            Span<Byte> finalBlock = new Byte[padLength + 8];
            finalBlock[0] = 0x80; // Leading 1 bit

            // Push message length as big-endian 64-bit integer
            BinaryPrimitives.WriteUInt64BigEndian(finalBlock[padLength..], bits);

            // Process the final block(s)
            Update(finalBlock[..(padLength + 8)]);

            // Store the final hash (Only store the first 28 bytes for SHA-224)
            _finalHash = new Byte[28]; // SHA-224 is 224 bits = 28 bytes

            // Convert state to bytes (big-endian)
            for (Int32 i = 0; i < 7; i++) // Only 7 integers for SHA-224 (224/32 = 7)
            {
                BinaryPrimitives.WriteUInt32BigEndian(_finalHash.AsSpan(i * 4), _state[i]);
            }

            _isFinalized = true;
        }

        return (Byte[])_finalHash.Clone();
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Byte[] ComputeHash(ReadOnlySpan<Byte> data)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(SHA224));
        Update(data);
        return FinalizeHash();
    }

    #endregion Public Methods

    #region SHA-224/256 Functions

    /// <summary>
    /// Processes a 64-byte block of data.
    /// </summary>
    /// <param name="block">The 64-byte block to process.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void ProcessBlock(ReadOnlySpan<Byte> block)
    {
        if (block.Length != 64)
        {
            throw new ArgumentException("Block size must be 64 bytes", nameof(block));
        }

        fixed (Byte* ptr = block)
        fixed (UInt32* w = _w)
        {
            // Load first 16 words (big-endian)
            for (Int32 t = 0; t < 16; t++)
            {
                UInt32 value = Unsafe.ReadUnaligned<UInt32>(ptr + (t * 4));
                w[t] = BinaryPrimitives.ReverseEndianness(value);
            }

            // Expand W[16..63]
            for (Int32 t = 16; t < 64; t++)
            {
                w[t] = BitwiseUtils.Sigma1(w[t - 2]) + w[t - 7] + BitwiseUtils.Sigma0(w[t - 15]) + w[t - 16];
            }
        }

        // Initialize working variables
        UInt32 a = _state[0]; UInt32 b = _state[1]; UInt32 c = _state[2]; UInt32 d = _state[3];
        UInt32 e = _state[4]; UInt32 f = _state[5]; UInt32 g = _state[6]; UInt32 h = _state[7];

        // Main compression loop
        for (Int32 t = 0; t < 64; t++)
        {
            UInt32 t1 = h + BitwiseUtils.SigmaUpper1(e) + BitwiseUtils.Choose(e, f, g) + SHA.K224[t] + _w[t];
            UInt32 t2 = BitwiseUtils.SigmaUpper0(a) + BitwiseUtils.Majority(a, b, c);

            h = g;
            g = f;
            f = e;
            e = d + t1;
            d = c;
            c = b;
            b = a;
            a = t1 + t2;
        }

        // Update hash state
        _state[0] += a; _state[1] += b; _state[2] += c; _state[3] += d;
        _state[4] += e; _state[5] += f; _state[6] += g; _state[7] += h;
    }

    #endregion SHA-224/256 Functions

    #region IDisposable

    /// <summary>
    /// Releases resources used by the SHA224 instance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

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

    #endregion IDisposable

    #region Overrides

    /// <summary>
    /// Returns a string representation of the SHA-224 hash algorithm.
    /// </summary>
    public override String ToString() => "SHA-224";

    #endregion Overrides
}
