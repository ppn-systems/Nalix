using Nalix.Common.Cryptography.Hashing;
using Nalix.Cryptography.Utilities;
using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace Nalix.Cryptography.Hashing;

/// <summary>
/// Provides an optimized implementation of the SHA-256 cryptographic hash algorithm using SIMD where available.
/// </summary>
/// <remarks>
/// This implementation processes data in 512-bit (64-byte) blocks, maintaining an internal state.
/// It supports incremental updates and can be used in a streaming manner.
/// </remarks>
[System.Runtime.InteropServices.ComVisible(true)]
public sealed class SHA256 : ISHA, IDisposable
{
    #region Fields

    private readonly byte[] _buffer = new byte[64];
    private readonly uint[] _state = new uint[8];

    private byte[] _finalHash;
    private int _bufferLength;
    private ulong _byteCount;
    private bool _finalized;
    private bool _disposed;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="SHA256"/> class and resets the hash state.
    /// </summary>
    public SHA256() => Initialize();

    #endregion Constructors

    #region Public Methods

    /// <summary>
    /// Resets the hash state to its initial values.
    /// </summary>
    /// <remarks>
    /// This method must be called before reusing an instance to compute a new hash.
    /// </remarks>
    public void Initialize()
    {
        Buffer.BlockCopy(SHA.H256, 0, _state, 0, SHA.H256.Length * sizeof(uint));

        _byteCount = 0;
        _bufferLength = 0;

        _disposed = false;
        _finalized = false;

        if (_finalHash != null)
        {
            Array.Clear(_finalHash, 0, _finalHash.Length);
            _finalHash = null;
        }
    }

    /// <summary>
    /// Computes the SHA-256 hash of the given data in a single call.
    /// </summary>
    /// <param name="data">The input data to hash.</param>
    /// <returns>The computed 256-bit hash as a byte array.</returns>
    /// <remarks>
    /// This method is a convenience wrapper that initializes, updates, and finalizes the hash computation.
    /// </remarks>
    public static byte[] HashData(ReadOnlySpan<byte> data)
    {
        using SHA256 sha256 = new();

        sha256.Initialize();
        sha256.Update(data);

        // Get the final hash and store it locally
        byte[] finalHash = sha256.FinalizeHash();

        // Create a new array to ensure we're not affected by disposal
        byte[] result = (byte[])finalHash.Clone();

        return result;
    }

    /// <summary>
    /// Computes the SHA-256 hash of the given data using an instance method.
    /// </summary>
    /// <param name="data">The input data to hash.</param>
    /// <returns>The computed 256-bit hash as a byte array.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    /// <remarks>
    /// This method allows incremental hashing by calling <see cref="Update"/> before finalizing with <see cref="FinalizeHash"/>.
    /// </remarks>
    public byte[] ComputeHash(ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(SHA256));

        // Process the data
        Update(data);

        // Get and verify the hash
        byte[] hash = FinalizeHash();

        // Create a defensive copy
        return (byte[])hash.Clone();
    }

    /// <summary>
    /// Updates the hash computation with a portion of data.
    /// </summary>
    /// <param name="data">The input data to process.</param>
    /// <exception cref="InvalidOperationException">Thrown if the hash has already been finalized.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    /// <remarks>
    /// This method processes data in 512-bit blocks and buffers any remaining bytes for the next update.
    /// </remarks>
    public void Update(ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(SHA256));

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
    /// Finalizes the hash computation and returns the resulting 256-bit hash.
    /// </summary>
    /// <returns>The final hash value as a 32-byte array.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    /// <remarks>
    /// Once finalized, the hash cannot be updated further. Calling this method multiple times returns the same result.
    /// </remarks>
    public byte[] FinalizeHash()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(SHA256));

        if (_finalized && _finalHash != null)
        {
            // Debug output for cached hash
            Console.WriteLine("Returning cached hash:");
            Console.WriteLine(BitConverter.ToString(_finalHash));
            return (byte[])_finalHash.Clone();
        }

        // Compute padding
        int remainder = (int)(_byteCount & 0x3F);
        int padLength = (remainder < 56) ? (56 - remainder) : (120 - remainder);

        Span<byte> padding = stackalloc byte[64];
        padding[0] = 0x80;
        padding[1..padLength].Clear();

        // Append length in bits
        Span<byte> lengthBytes = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(lengthBytes, _byteCount * 8);

        Update(padding[..padLength]);
        Update(lengthBytes);

        // Create new hash array
        byte[] hash = new byte[32];

        // Convert state to bytes with proper endianness
        for (int i = 0; i < 8; i++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(hash.AsSpan(i * 4), _state[i]);
        }

        // Store and mark as finalized
        _finalHash = hash;
        _finalized = true;

        return hash;
    }

    /// <summary>
    /// Gets the computed hash value after finalization.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if <see cref="FinalizeHash"/> has not been called yet.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    public byte[] Hash
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(SHA256));

            if (!_finalized)
                throw new InvalidOperationException(
                    "The hash has not been completed. Call TransformFinalBlock before accessing the Hashing.");
            return (byte[])_finalHash.Clone();
        }
    }

    /// <summary>
    /// Updates the hash state with a block of data and optionally copies the data to an output buffer.
    /// </summary>
    /// <param name="inputBuffer">The input buffer containing data.</param>
    /// <param name="inputOffset">The offset in the input buffer where data begins.</param>
    /// <param name="inputCount">The Number of bytes to process.</param>
    /// <param name="outputBuffer">The buffer to copy input data into (can be null).</param>
    /// <param name="outputOffset">The offset in the output buffer.</param>
    /// <returns>The Number of bytes processed.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="inputBuffer"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if <paramref name="inputOffset"/> or <paramref name="inputCount"/> are invalid.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    public int TransformBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(SHA256));

        ArgumentNullException.ThrowIfNull(inputBuffer);
        if (inputOffset < 0 || inputCount < 0 || inputOffset + inputCount > inputBuffer.Length)
            throw new ArgumentOutOfRangeException(nameof(inputOffset), "The input offset or count is out of range.");

        Update(new ReadOnlySpan<byte>(inputBuffer, inputOffset, inputCount));

        if (outputBuffer != null)
            Buffer.BlockCopy(inputBuffer, inputOffset, outputBuffer, outputOffset, inputCount);

        return inputCount;
    }

    /// <summary>
    /// Processes the final block of data and returns it.
    /// </summary>
    /// <param name="inputBuffer">The input buffer containing data.</param>
    /// <param name="inputOffset">The offset in the input buffer where data begins.</param>
    /// <param name="inputCount">The Number of bytes to process.</param>
    /// <returns>A copy of the final processed block.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="inputBuffer"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if <paramref name="inputOffset"/> or <paramref name="inputCount"/> are invalid.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    /// <remarks>
    /// This method calls <see cref="Update"/> with the final block and then finalizes the hash.
    /// </remarks>
    public byte[] TransformFinalBlock(byte[] inputBuffer, int inputOffset, int inputCount)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(SHA256));

        ArgumentNullException.ThrowIfNull(inputBuffer);
        if (inputOffset < 0 || inputCount < 0 || inputOffset + inputCount > inputBuffer.Length)
            throw new ArgumentOutOfRangeException(nameof(inputOffset), "The input offset or count is out of range.");

        byte[] finalBlock = new byte[inputCount];
        Buffer.BlockCopy(inputBuffer, inputOffset, finalBlock, 0, inputCount);
        Update(finalBlock);
        _finalHash = FinalizeHash();
        return finalBlock;
    }

    #endregion Public Methods

    #region Private Methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void ProcessBlock(ReadOnlySpan<byte> block)
    {
        if (block.Length < 64)
            throw new ArgumentException($"Invalid block size: {block.Length}", nameof(block));

        uint* w = stackalloc uint[64];

        // Save initial state
        uint a = _state[0], b = _state[1], c = _state[2], d = _state[3];
        uint e = _state[4], f = _state[5], g = _state[6], h = _state[7];

        // Load message words (big-endian)
        for (int i = 0; i < 16; i++)
            w[i] = BinaryPrimitives.ReadUInt32BigEndian(block[(i * 4)..]);

        // Message schedule expansion
        for (int i = 16; i < 64; i++)
        {
            uint s0 = BitwiseUtils.RotateRight(w[i - 15], 7) ^
                     BitwiseUtils.RotateRight(w[i - 15], 18) ^
                     (w[i - 15] >> 3);
            uint s1 = BitwiseUtils.RotateRight(w[i - 2], 17) ^
                     BitwiseUtils.RotateRight(w[i - 2], 19) ^
                     (w[i - 2] >> 10);
            w[i] = w[i - 16] + s0 + w[i - 7] + s1;
        }

        // Save original state for update
        uint origA = a, origB = b, origC = c, origD = d;
        uint origE = e, origF = f, origG = g, origH = h;

        // Main compression function
        for (int i = 0; i < 64; i++)
        {
            uint S1 = Sigma1(e);
            uint ch = BitwiseUtils.Choose(e, f, g);
            uint temp1 = h + S1 + ch + SHA.K256[i] + w[i];
            uint S0 = Sigma0(a);
            uint maj = BitwiseUtils.Majority(a, b, c);
            uint temp2 = S0 + maj;

            h = g;
            g = f;
            f = e;
            e = d + temp1;
            d = c;
            c = b;
            b = a;
            a = temp1 + temp2;
        }

        // Correct state update
        _state[0] = origA + a;
        _state[1] = origB + b;
        _state[2] = origC + c;
        _state[3] = origD + d;
        _state[4] = origE + e;
        _state[5] = origF + f;
        _state[6] = origG + g;
        _state[7] = origH + h;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Sigma1(uint e)
        => BitwiseUtils.RotateRight(e, 6) ^ BitwiseUtils.RotateRight(e, 11) ^ BitwiseUtils.RotateRight(e, 25);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Sigma0(uint a)
        => BitwiseUtils.RotateRight(a, 2) ^ BitwiseUtils.RotateRight(a, 13) ^ BitwiseUtils.RotateRight(a, 22);

    #endregion Private Methods

    #region IDisposable

    /// <summary>
    /// Releases all resources used by the <see cref="SHA256"/> instance.
    /// </summary>
    /// <remarks>
    /// This method clears sensitive data from memory and marks the instance as disposed.
    /// </remarks>
    public void Dispose()
    {
        if (_disposed) return;

        // Clear sensitive data from memory
        Array.Clear(_buffer, 0, _buffer.Length);
        Array.Clear(_state, 0, _state.Length);

        // Don't clear _finalHash until we're sure it's been returned
        if (_finalHash != null) Array.Clear(_finalHash, 0, _finalHash.Length);

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    #endregion IDisposable

    #region Overrides

    /// <summary>
    /// Returns a string representation of the SHA-256 hash algorithm.
    /// </summary>
    public override string ToString() => "SHA-256";

    #endregion Overrides
}
