using Nalix.Common.Security.Cryptography.Hashing;
using Nalix.Cryptography.Internal;
using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Nalix.Cryptography.Hashing;

/// <summary>
/// Provides an optimized implementation of the SHA-256 cryptographic hash algorithm using SIMD where available.
/// </summary>
/// <remarks>
/// This implementation processes data in 512-bit (64-byte) blocks, maintaining an internal state.
/// It supports incremental updates and can be used in a streaming manner.
/// </remarks>
[System.Runtime.InteropServices.ComVisible(true)]
public sealed class SHA256 : IShaDigest, IDisposable
{
    #region Fields

    private readonly Byte[] _buffer = new Byte[64];
    private readonly UInt32[] _state = new UInt32[8];

    private Byte[] _finalHash;
    private Int32 _bufferLength;
    private UInt64 _byteCount;
    private Boolean _finalized;
    private Boolean _disposed;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="SHA256"/> class and resets the hash state.
    /// </summary>
    public SHA256() => Initialize();

    #endregion Constructors

    #region Public Methods

    /// <summary>
    /// Computes the SHA-256 hash of the given data in a single call.
    /// </summary>
    /// <param name="data">The input data to hash.</param>
    /// <returns>The computed 256-bit hash as a byte array.</returns>
    /// <remarks>
    /// This method is a convenience wrapper that initializes, updates, and finalizes the hash computation.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Byte[] HashData(ReadOnlySpan<Byte> data)
    {
        using SHA256 sha256 = new();
        sha256.Update(data);
        Byte[] hash = sha256.FinalizeHash();
        Byte[] result = GC.AllocateUninitializedArray<Byte>(hash.Length);
        hash.CopyTo(result, 0);
        return result;
    }

    /// <summary>
    /// Computes the SHA-256 hash of the input data and writes the result to the provided output buffer.
    /// </summary>
    /// <param name="data">The input data to hash.</param>
    /// <param name="output">The output buffer where the 32-byte hash will be written. Must be at least 32 bytes long.</param>
    /// <exception cref="ArgumentException">Thrown if the output buffer is smaller than 32 bytes.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void HashData(ReadOnlySpan<Byte> data, Span<Byte> output)
    {
        if (output.Length < 32)
        {
            throw new ArgumentException("Output buffer must be at least 32 bytes", nameof(output));
        }

        using SHA256 sha256 = new();
        sha256.Update(data);
        sha256.FinalizeHash(output);
    }

    /// <summary>
    /// Resets the hash state to its initial values.
    /// </summary>
    /// <remarks>
    /// This method must be called before reusing an instance to compute a new hash.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Initialize()
    {
        unsafe
        {
            ref Byte src = ref Unsafe.As<UInt32, Byte>(ref MemoryMarshal.GetArrayDataReference(SHA.H256));
            ref Byte dst = ref Unsafe.As<UInt32, Byte>(ref MemoryMarshal.GetArrayDataReference(_state));
            Unsafe.CopyBlockUnaligned(ref dst, ref src, (UInt32)(SHA.H256.Length * sizeof(UInt32)));
        }

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
    /// Computes the SHA-256 hash of the given data using an instance method.
    /// </summary>
    /// <param name="data">The input data to hash.</param>
    /// <returns>The computed 256-bit hash as a byte array.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    /// <remarks>
    /// This method allows incremental hashing by calling <see cref="Update"/> before finalizing with <see cref="FinalizeHash()"/>.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Byte[] ComputeHash(ReadOnlySpan<Byte> data)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(SHA256));

        // Process the data
        Update(data);
        Byte[] result = GC.AllocateUninitializedArray<Byte>(32);
        FinalizeHash().AsSpan().CopyTo(result);
        return result;
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update(ReadOnlySpan<Byte> data)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(SHA256));

        if (_finalized)
        {
            throw new InvalidOperationException("Cannot update after finalization.");
        }

        ReadOnlySpan<Byte> input = data;

        // Fill buffer if partially full
        if (_bufferLength > 0)
        {
            Int32 space = 64 - _bufferLength;
            Int32 toCopy = Math.Min(space, input.Length);

            input[..toCopy].CopyTo(_buffer.AsSpan(_bufferLength));
            _bufferLength += toCopy;
            input = input[toCopy..];

            if (_bufferLength == 64)
            {
                ProcessBlock(_buffer);
                _bufferLength = 0;
            }
        }

        // Process full 64-byte blocks
        while (input.Length >= 64)
        {
            ProcessBlock(input[..64]);
            input = input[64..];
        }

        // Buffer remainder
        if (!input.IsEmpty)
        {
            input.CopyTo(_buffer.AsSpan(_bufferLength));
            _bufferLength += input.Length;
        }

        _byteCount += (UInt64)data.Length;
    }

    /// <summary>
    /// Finalizes the hash computation and writes the resulting 256-bit hash to the provided output buffer.
    /// </summary>
    /// <param name="output">The output buffer where the 32-byte hash will be written. Must be at least 32 bytes long.</param>
    /// <exception cref="ArgumentException">Thrown if the output buffer is smaller than 32 bytes.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    /// <remarks>
    /// Once finalized, the hash cannot be updated further. Calling this method multiple times returns the same result.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FinalizeHash(Span<Byte> output)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(SHA256));

        if (output.Length < 32)
        {
            throw new ArgumentException("Output buffer must be at least 32 bytes", nameof(output));
        }

        if (_finalized)
        {
            _finalHash.AsSpan().CopyTo(output);
            return;
        }

        // Compute padding
        Int32 remainder = (Int32)(_byteCount & 0x3F);
        Int32 padLength = (remainder < 56) ? (56 - remainder) : (120 - remainder);

        Span<Byte> finalBlock = stackalloc Byte[128];
        finalBlock.Clear();
        finalBlock[0] = 0x80;

        // Append length in bits
        BinaryPrimitives.WriteUInt64BigEndian(finalBlock.Slice(padLength, 8), _byteCount * 8);

        Update(finalBlock[..(padLength + 8)]);

        // Write final state to output buffer
        unsafe
        {
            fixed (Byte* dst = output)
            {
                UInt32* outPtr = (UInt32*)dst;
                for (Int32 i = 0; i < 8; i++)
                {
                    outPtr[i] = BitConverter.IsLittleEndian
                        ? BinaryPrimitives.ReverseEndianness(_state[i])
                        : _state[i];
                }
            }
        }

        // Cache hash internally for .Hash and fallback use
        _finalHash = GC.AllocateUninitializedArray<Byte>(32);
        output.CopyTo(_finalHash);
        _finalized = true;
    }

    /// <summary>
    /// Finalizes the hash computation and returns the resulting 256-bit hash.
    /// </summary>
    /// <returns>The final hash value as a 32-byte array.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    /// <remarks>
    /// Once finalized, the hash cannot be updated further. Calling this method multiple times returns the same result.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Byte[] FinalizeHash()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(SHA256));

        if (_finalized && _finalHash != null)
        {
            return _finalHash;
        }

        // Compute padding
        Int32 remainder = (Int32)(_byteCount & 0x3F);
        Int32 padLength = (remainder < 56) ? (56 - remainder) : (120 - remainder);

        Span<Byte> finalBlock = stackalloc Byte[128];
        finalBlock.Clear();
        finalBlock[0] = 0x80;

        // Append length in bits
        BinaryPrimitives.WriteUInt64BigEndian(finalBlock.Slice(padLength, 8), _byteCount * 8);

        // Single update call
        Update(finalBlock[..(padLength + 8)]);

        // Create new hash array
        Byte[] hash = new Byte[32];

        // Convert to big-endian hash output
        unsafe
        {
            fixed (Byte* p = hash)
            {
                UInt32* u = (UInt32*)p;
                for (Int32 i = 0; i < 8; i++)
                {
                    u[i] = BitConverter.IsLittleEndian
                        ? BinaryPrimitives.ReverseEndianness(_state[i]) : _state[i];
                }
            }
        }

        // Store and mark as finalized
        _finalHash = hash;
        _finalized = true;

        return _finalHash;
    }

    /// <summary>
    /// Gets the computed hash value after finalization.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if <see cref="FinalizeHash()"/> has not been called yet.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    public Byte[] Hash
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(SHA256));

            return !_finalized
                ? throw new InvalidOperationException(
                    "The hash has not been completed. Call TransformFinalBlock before accessing the Hashing.")
                : (Byte[])_finalHash.Clone();
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Int32 TransformBlock(
        Byte[] inputBuffer,
        Int32 inputOffset,
        Int32 inputCount,
        Byte[] outputBuffer,
        Int32 outputOffset)
    {
        ArgumentNullException.ThrowIfNull(inputBuffer);
        ObjectDisposedException.ThrowIf(_disposed, nameof(SHA256));

        if ((UInt32)inputOffset > inputBuffer.Length || (UInt32)inputCount > inputBuffer.Length - inputOffset)
        {
            throw new ArgumentOutOfRangeException(nameof(inputOffset));
        }

        Update(new ReadOnlySpan<Byte>(inputBuffer, inputOffset, inputCount));

        if (outputBuffer != null)
        {
            Buffer.BlockCopy(inputBuffer, inputOffset, outputBuffer, outputOffset, inputCount);
        }

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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TransformFinalBlock(Byte[] inputBuffer, Int32 inputOffset, Int32 inputCount)
    {
        ArgumentNullException.ThrowIfNull(inputBuffer);
        ObjectDisposedException.ThrowIf(_disposed, nameof(SHA256));

        if ((UInt32)inputOffset > inputBuffer.Length ||
            (UInt32)inputCount > inputBuffer.Length - inputOffset)
        {
            throw new ArgumentOutOfRangeException(nameof(inputOffset));
        }

        Byte[] finalBlock = GC.AllocateUninitializedArray<Byte>(inputCount);
        Buffer.BlockCopy(inputBuffer, inputOffset, finalBlock, 0, inputCount);

        Update(finalBlock);
        _finalHash = FinalizeHash();
    }

    #endregion Public Methods

    #region Private Methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void ProcessBlock(ReadOnlySpan<Byte> block)
    {
        if (block.Length < 64)
        {
            throw new ArgumentException($"Invalid block size: {block.Length}", nameof(block));
        }

        UInt32* w = stackalloc UInt32[64];

        fixed (Byte* ptr = block)
        {
            for (Int32 i = 0; i < 16; i++)
            {
                UInt32 raw = Unsafe.ReadUnaligned<UInt32>(ptr + (i << 2)); // i * 4
                w[i] = BinaryPrimitives.ReverseEndianness(raw);
            }
            for (Int32 i = 16; i < 64; i++)
            {
                w[i] = w[i - 16] + BitwiseUtils.Sigma0(w[i - 15]) + w[i - 7] + BitwiseUtils.Sigma1(w[i - 2]);
            }
        }

        // Initialize working variables
        UInt32 a = _state[0], b = _state[1], c = _state[2], d = _state[3];
        UInt32 e = _state[4], f = _state[5], g = _state[6], h = _state[7];

        var Ch = BitwiseUtils.Choose;
        var Maj = BitwiseUtils.Majority;
        var S0 = BitwiseUtils.SigmaUpper0;
        var S1 = BitwiseUtils.SigmaUpper1;

        // Compression loop
        for (Int32 i = 0; i < 64; i++)
        {
            UInt32 t1 = h + S1(e) + Ch(e, f, g) + SHA.K256[i] + w[i];
            UInt32 t2 = S0(a) + Maj(a, b, c);

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
        fixed (UInt32* s = _state)
        {
            s[0] += a; s[1] += b; s[2] += c; s[3] += d;
            s[4] += e; s[5] += f; s[6] += g; s[7] += h;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe UInt32 Reverse(Byte* ptr) =>
        BinaryPrimitives.ReverseEndianness(Unsafe.ReadUnaligned<UInt32>(ptr));

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
        if (_disposed)
        {
            return;
        }

        // Clear sensitive data from memory
        Array.Clear(_buffer, 0, _buffer.Length);
        Array.Clear(_state, 0, _state.Length);

        // Don't clear _finalHash until we're sure it's been returned
        if (_finalHash != null)
        {
            Array.Clear(_finalHash, 0, _finalHash.Length);
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    #endregion IDisposable

    #region Overrides

    /// <summary>
    /// Returns a string representation of the SHA-256 hash algorithm.
    /// </summary>
    public override String ToString() => "SHA-256";

    #endregion Overrides
}