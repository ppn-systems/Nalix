// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Security.Abstractions;
using Nalix.Cryptography.Primitives;

namespace Nalix.Cryptography.Hashing;

/// <summary>
/// Provides an optimized implementation of the SHA-256 cryptographic hash algorithm using SIMD where available.
/// </summary>
/// <remarks>
/// This implementation processes data in 512-bit (64-byte) blocks, maintaining an internal state.
/// It supports incremental updates and can be used in a streaming manner.
/// </remarks>
[System.Runtime.InteropServices.ComVisible(true)]
[System.Diagnostics.DebuggerDisplay("Disposed={_disposed}, Finalized={_finalized}, Bytes={_byteCount}")]
public sealed class SHA256 : IShaDigest, System.IDisposable
{
    #region Fields

    private readonly System.Byte[] _buffer = new System.Byte[64];
    private readonly System.UInt32[] _state = new System.UInt32[8];

    private System.Byte[] _finalHash;
    private System.Int32 _bufferLength;
    private System.UInt64 _byteCount;
    private System.Boolean _finalized;
    private System.Boolean _disposed;

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
    [System.Diagnostics.DebuggerNonUserCode]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public void Initialize()
    {
        unsafe
        {
            ref System.Byte src = ref System.Runtime.CompilerServices.Unsafe.As<System.UInt32, System.Byte>(
                ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(SHA.H256));

            ref System.Byte dst = ref System.Runtime.CompilerServices.Unsafe.As<System.UInt32, System.Byte>(
                ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(_state));

            System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(
                ref dst, ref src, (System.UInt32)(SHA.H256.Length * sizeof(System.UInt32)));
        }

        _byteCount = 0;
        _bufferLength = 0;

        _disposed = false;
        _finalized = false;

        if (_finalHash != null)
        {
            System.Array.Clear(_finalHash, 0, _finalHash.Length);
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
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Byte[] HashData(System.ReadOnlySpan<System.Byte> data)
    {
        using SHA256 sha256 = new();
        sha256.Update(data);
        System.Byte[] hash = sha256.FinalizeHash();
        System.Byte[] result = System.GC.AllocateUninitializedArray<System.Byte>(hash.Length);
        hash.CopyTo(result, 0);
        return result;
    }

    /// <summary>
    /// Computes the SHA-256 hash of the input data and writes the result to the provided output buffer.
    /// </summary>
    /// <param name="data">The input data to hash.</param>
    /// <param name="output">The output buffer where the 32-byte hash will be written. Must be at least 32 bytes long.</param>
    /// <exception cref="System.ArgumentException">Thrown if the output buffer is smaller than 32 bytes.</exception>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void HashData(System.ReadOnlySpan<System.Byte> data, System.Span<System.Byte> output)
    {
        if (output.Length < 32)
        {
            throw new System.ArgumentException("Output buffer must be at least 32 bytes", nameof(output));
        }

        using SHA256 sha256 = new();
        sha256.Update(data);
        sha256.FinalizeHash(output);
    }

    /// <summary>
    /// Computes the SHA-256 hash of the given data using an instance method.
    /// </summary>
    /// <param name="data">The input data to hash.</param>
    /// <returns>The computed 256-bit hash as a byte array.</returns>
    /// <exception cref="System.ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    /// <remarks>
    /// This method allows incremental hashing by calling <see cref="Update"/> before finalizing with <see cref="FinalizeHash()"/>.
    /// </remarks>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Byte[] ComputeHash(System.ReadOnlySpan<System.Byte> data)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(SHA256));

        // Process the data
        Update(data);
        System.Byte[] result = System.GC.AllocateUninitializedArray<System.Byte>(32);
        System.MemoryExtensions.AsSpan(FinalizeHash()).CopyTo(result);
        return result;
    }

    /// <summary>
    /// Updates the hash computation with a portion of data.
    /// </summary>
    /// <param name="data">The input data to process.</param>
    /// <exception cref="System.InvalidOperationException">Thrown if the hash has already been finalized.</exception>
    /// <exception cref="System.ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    /// <remarks>
    /// This method processes data in 512-bit blocks and buffers any remaining bytes for the next update.
    /// </remarks>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Update(System.ReadOnlySpan<System.Byte> data)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(SHA256));

        if (_finalized)
        {
            throw new System.InvalidOperationException("Cannot update after finalization.");
        }

        System.ReadOnlySpan<System.Byte> input = data;

        // Fill buffer if partially full
        if (_bufferLength > 0)
        {
            System.Int32 space = 64 - _bufferLength;
            System.Int32 toCopy = System.Math.Min(space, input.Length);

            input[..toCopy].CopyTo(System.MemoryExtensions.AsSpan(_buffer, _bufferLength));
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
            input.CopyTo(System.MemoryExtensions.AsSpan(_buffer, _bufferLength));
            _bufferLength += input.Length;
        }

        _byteCount += (System.UInt64)data.Length;
    }

    /// <summary>
    /// Finalizes the hash computation and writes the resulting 256-bit hash to the provided output buffer.
    /// </summary>
    /// <param name="output">The output buffer where the 32-byte hash will be written. Must be at least 32 bytes long.</param>
    /// <exception cref="System.ArgumentException">Thrown if the output buffer is smaller than 32 bytes.</exception>
    /// <exception cref="System.ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    /// <remarks>
    /// Once finalized, the hash cannot be updated further. Calling this method multiple times returns the same result.
    /// </remarks>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void FinalizeHash(System.Span<System.Byte> output)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(SHA256));

        if (output.Length < 32)
        {
            throw new System.ArgumentException("Output buffer must be at least 32 bytes", nameof(output));
        }

        if (_finalized)
        {
            System.MemoryExtensions.AsSpan(_finalHash).CopyTo(output);
            return;
        }

        // Compute padding
        System.Int32 remainder = (System.Int32)(_byteCount & 0x3F);
        System.Int32 padLength = (remainder < 56) ? (56 - remainder) : (120 - remainder);

        System.Span<System.Byte> finalBlock = stackalloc System.Byte[128];
        finalBlock.Clear();
        finalBlock[0] = 0x80;

        // Append length in bits
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(
            finalBlock.Slice(padLength, 8), _byteCount * 8);

        Update(finalBlock[..(padLength + 8)]);

        // Write final state to output buffer
        unsafe
        {
            fixed (System.Byte* dst = output)
            {
                System.UInt32* outPtr = (System.UInt32*)dst;
                for (System.Int32 i = 0; i < 8; i++)
                {
                    outPtr[i] = System.BitConverter.IsLittleEndian
                        ? System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(_state[i])
                        : _state[i];
                }
            }
        }

        // Cache hash internally for .Hash and fallback use
        _finalHash = System.GC.AllocateUninitializedArray<System.Byte>(32);
        output.CopyTo(_finalHash);
        _finalized = true;
    }

    /// <summary>
    /// Finalizes the hash computation and returns the resulting 256-bit hash.
    /// </summary>
    /// <returns>The final hash value as a 32-byte array.</returns>
    /// <exception cref="System.ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    /// <remarks>
    /// Once finalized, the hash cannot be updated further. Calling this method multiple times returns the same result.
    /// </remarks>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Byte[] FinalizeHash()
    {
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(SHA256));

        if (_finalized && _finalHash != null)
        {
            return _finalHash;
        }

        // Compute padding
        System.Int32 remainder = (System.Int32)(_byteCount & 0x3F);
        System.Int32 padLength = (remainder < 56) ? (56 - remainder) : (120 - remainder);

        System.Span<System.Byte> finalBlock = stackalloc System.Byte[128];
        finalBlock.Clear();
        finalBlock[0] = 0x80;

        // Append length in bits
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(
            finalBlock.Slice(padLength, 8), _byteCount * 8);

        // Single update call
        Update(finalBlock[..(padLength + 8)]);

        // Create new hash array
        System.Byte[] hash = new System.Byte[32];

        // Convert to big-endian hash output
        unsafe
        {
            fixed (System.Byte* p = hash)
            {
                System.UInt32* u = (System.UInt32*)p;
                for (System.Int32 i = 0; i < 8; i++)
                {
                    u[i] = System.BitConverter.IsLittleEndian
                        ? System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(_state[i]) : _state[i];
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
    /// <exception cref="System.InvalidOperationException">
    /// Thrown if <see cref="FinalizeHash()"/> has not been called yet.
    /// </exception>
    /// <exception cref="System.ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    public System.Byte[] Hash
    {
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        get
        {
            System.ObjectDisposedException.ThrowIf(_disposed, nameof(SHA256));

            return !_finalized
                ? throw new System.InvalidOperationException(
                    "Hash is not available yet. Call FinalizeHash() (or TransformFinalBlock) before accessing Hash.")
                : (System.Byte[])_finalHash.Clone();
        }
    }

    /// <summary>
    /// Updates the hash state with a block of data and optionally copies the data to an output buffer.
    /// </summary>
    /// <param name="inputBuffer">The input buffer containing data.</param>
    /// <param name="inputOffset">The offset in the input buffer where data begins.</param>
    /// <param name="inputCount">The ProtocolType of bytes to process.</param>
    /// <param name="outputBuffer">The buffer to copy input data into (can be null).</param>
    /// <param name="outputOffset">The offset in the output buffer.</param>
    /// <returns>The ProtocolType of bytes processed.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="inputBuffer"/> is null.</exception>
    /// <exception cref="System.ArgumentOutOfRangeException">
    /// Thrown if <paramref name="inputOffset"/> or <paramref name="inputCount"/> are invalid.
    /// </exception>
    /// <exception cref="System.ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Advanced)]
    public System.Int32 TransformBlock(System.Byte[] inputBuffer, System.Int32 inputOffset,
        System.Int32 inputCount, System.Byte[] outputBuffer, System.Int32 outputOffset)
    {
        System.ArgumentNullException.ThrowIfNull(inputBuffer);
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(SHA256));

        if ((System.UInt32)inputOffset > inputBuffer.Length ||
            (System.UInt32)inputCount > inputBuffer.Length - inputOffset)
        {
            throw new System.ArgumentOutOfRangeException(nameof(inputOffset));
        }

        Update(new System.ReadOnlySpan<System.Byte>(inputBuffer, inputOffset, inputCount));

        if (outputBuffer != null)
        {
            System.Buffer.BlockCopy(inputBuffer, inputOffset, outputBuffer, outputOffset, inputCount);
        }

        return inputCount;
    }

    /// <summary>
    /// Processes the final block of data and returns it.
    /// </summary>
    /// <param name="inputBuffer">The input buffer containing data.</param>
    /// <param name="inputOffset">The offset in the input buffer where data begins.</param>
    /// <param name="inputCount">The ProtocolType of bytes to process.</param>
    /// <returns>A copy of the final processed block.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="inputBuffer"/> is null.</exception>
    /// <exception cref="System.ArgumentOutOfRangeException">
    /// Thrown if <paramref name="inputOffset"/> or <paramref name="inputCount"/> are invalid.
    /// </exception>
    /// <exception cref="System.ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    /// <remarks>
    /// This method calls <see cref="Update"/> with the final block and then finalizes the hash.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Advanced)]
    public void TransformFinalBlock(System.Byte[] inputBuffer, System.Int32 inputOffset, System.Int32 inputCount)
    {
        System.ArgumentNullException.ThrowIfNull(inputBuffer);
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(SHA256));

        if ((System.UInt32)inputOffset > inputBuffer.Length ||
            (System.UInt32)inputCount > inputBuffer.Length - inputOffset)
        {
            throw new System.ArgumentOutOfRangeException(nameof(inputOffset));
        }

        this.Update(new System.ReadOnlySpan<System.Byte>(inputBuffer, inputOffset, inputCount));

        _finalHash = this.FinalizeHash();
    }

    #endregion Public Methods

    #region Private Methods

    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.SkipLocalsInit]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private unsafe void ProcessBlock(System.ReadOnlySpan<System.Byte> block)
    {
        if (block.Length < 64)
        {
            throw new System.ArgumentException($"Invalid block size: {block.Length}", nameof(block));
        }

        System.UInt32* w = stackalloc System.UInt32[64];

        fixed (System.Byte* ptr = block)
        {
            for (System.Int32 i = 0; i < 16; i++)
            {
                System.UInt32 raw = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<
                    System.UInt32>(ptr + (i << 2)); // i * 4
                w[i] = System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(raw);
            }
            for (System.Int32 i = 16; i < 64; i++)
            {
                w[i] = w[i - 16] + BitwiseUtils.Sigma0(w[i - 15]) + w[i - 7] + BitwiseUtils.Sigma1(w[i - 2]);
            }
        }

        // Initialize working variables
        System.UInt32 a = _state[0], b = _state[1], c = _state[2], d = _state[3];
        System.UInt32 e = _state[4], f = _state[5], g = _state[6], h = _state[7];

        var Ch = BitwiseUtils.Choose;
        var Maj = BitwiseUtils.Majority;
        var S0 = BitwiseUtils.SigmaUpper0;
        var S1 = BitwiseUtils.SigmaUpper1;

        // Compression loop
        for (System.Int32 i = 0; i < 64; i++)
        {
            System.UInt32 t1 = h + S1(e) + Ch(e, f, g) + SHA.K256[i] + w[i];
            System.UInt32 t2 = S0(a) + Maj(a, b, c);

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
        fixed (System.UInt32* s = _state)
        {
            s[0] += a; s[1] += b; s[2] += c; s[3] += d;
            s[4] += e; s[5] += f; s[6] += g; s[7] += h;
        }
    }

    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static unsafe System.UInt32 Reverse(System.Byte* ptr) =>
        System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(
            System.Runtime.CompilerServices.Unsafe.ReadUnaligned<System.UInt32>(ptr));

    #endregion Private Methods

    #region IDisposable

    /// <summary>
    /// Releases all resources used by the <see cref="SHA256"/> instance.
    /// </summary>
    /// <remarks>
    /// This method clears sensitive data from memory and marks the instance as disposed.
    /// </remarks>
    [System.Diagnostics.DebuggerNonUserCode]
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        // Clear sensitive data from memory
        System.Array.Clear(_buffer, 0, _buffer.Length);
        System.Array.Clear(_state, 0, _state.Length);

        // Don't clear _finalHash until we're sure it's been returned
        if (_finalHash != null)
        {
            System.Array.Clear(_finalHash, 0, _finalHash.Length);
        }

        _disposed = true;
        System.GC.SuppressFinalize(this);
    }

    #endregion IDisposable

    #region Overrides

    /// <summary>
    /// Returns a string representation of the SHA-256 hash algorithm.
    /// </summary>
    public override System.String ToString() => "SHA-256";

    #endregion Overrides
}