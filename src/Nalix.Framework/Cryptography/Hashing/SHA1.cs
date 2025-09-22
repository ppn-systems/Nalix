// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Abstractions;
using Nalix.Cryptography.Primitives;

namespace Nalix.Framework.Cryptography.Hashing;

/// <summary>
/// Provides an optimized implementation of the SHA-1 cryptographic hash algorithm.
/// </summary>
/// <remarks>
/// SHA-1 is a cryptographic hash function that produces a 160-bit (20-byte) hash value.
/// It is considered weak due to known vulnerabilities but is still used in legacy systems.
/// This implementation processes data in 512-bit (64-byte) blocks.
/// </remarks>
[System.Runtime.InteropServices.ComVisible(true)]
public sealed class SHA1 : IShaDigest, System.IDisposable
{
    #region Fields

    // Hash state instance field
    private readonly System.UInt32[] _state = new System.UInt32[5];

    private System.Boolean _disposed = false;

    // Fields for incremental hashing
    private readonly System.Byte[] _buffer = new System.Byte[64]; // Buffer for incomplete blocks

    private System.Int32 _bufferIndex = 0;                  // Current position in buffer
    private System.UInt64 _totalBytesProcessed = 0;        // Total bytes processed
    private System.Boolean _finalized = false;               // Flag indicating hash has been finalized

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
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Byte[] HashData(System.ReadOnlySpan<System.Byte> data)
    {
        using SHA1 sha1 = new();
        sha1.Update(data);
        return sha1.FinalizeHash();
    }

    /// <summary>
    /// Resets the hash state to initial values.
    /// </summary>
    [System.Diagnostics.DebuggerNonUserCode]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public unsafe void Initialize()
    {
        fixed (System.UInt32* src = SHA.H1, dst = _state)
        {
            for (System.Int32 i = 0; i < SHA.H1.Length; i++)
            {
                dst[i] = src[i];
            }
        }

        _bufferIndex = 0;
        _totalBytesProcessed = 0;
        _finalized = false;
    }

    /// <summary>
    /// Updates the hash with more data.
    /// </summary>
    /// <param name="data">The input data to process.</param>
    /// <exception cref="System.ObjectDisposedException">
    /// Thrown if this method is called after the object has been disposed.
    /// </exception>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown if this method is called after the hash has been finalized.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Update(System.ReadOnlySpan<System.Byte> data)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(SHA1));

        if (_finalized)
        {
            throw new System.InvalidOperationException("Hash has been finalized");
        }

        _totalBytesProcessed += (System.UInt64)data.Length;

        // Process any bytes still in the buffer
        if (_bufferIndex > 0)
        {
            System.Int32 bytesToCopy = System.Math.Min(64 - _bufferIndex, data.Length);
            data[..bytesToCopy].CopyTo(System.MemoryExtensions.AsSpan(_buffer)[_bufferIndex..]);
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
            data.CopyTo(System.MemoryExtensions.AsSpan(_buffer)[_bufferIndex..]);
            _bufferIndex += data.Length;
        }
    }

    /// <summary>
    /// Finalizes the hash computation and returns the hash value.
    /// </summary>
    /// <returns>The computed hash.</returns>
    /// <exception cref="System.ObjectDisposedException">
    /// Thrown if this method is called after the object has been disposed.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Byte[] FinalizeHash()
    {
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(SHA1));

        System.Byte[] result = new System.Byte[20];

        if (_finalized)
        {
            // Create a copy of the hash result without reprocessing
            unsafe
            {
                fixed (System.Byte* p = result)
                {
                    System.UInt32* ptr = (System.UInt32*)p;
                    for (System.Int32 i = 0; i < 5; i++)
                    {
                        ptr[i] = System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(_state[i]);
                    }
                }
            }

            return result;
        }

        // Calculate message length in bits
        System.UInt64 bitLength = _totalBytesProcessed * 8;

        // Push padding as in ComputeHash method
        System.Span<System.Byte> paddingBuffer = stackalloc System.Byte[128]; // Max 2 blocks needed
        System.Int32 paddingBufferPos = 0;

        // Copy remaining data from buffer
        if (_bufferIndex > 0)
        {
            System.MemoryExtensions.AsSpan(_buffer, 0, _bufferIndex)
                .CopyTo(paddingBuffer);
            paddingBufferPos = _bufferIndex;
        }

        // Push the '1' bit
        paddingBuffer[paddingBufferPos++] = 0x80;

        // Determine if we need one or two blocks
        System.Int32 blockCount = paddingBufferPos + 8 > 64 ? 2 : 1;
        System.Int32 finalBlockSize = blockCount * 64;

        // Fill with zeros until length field
        while (paddingBufferPos < finalBlockSize - 8)
        {
            paddingBuffer[paddingBufferPos++] = 0;
        }

        // WriteInt16 the length in bits as a 64-bit big-endian integer
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(
            paddingBuffer[(finalBlockSize - 8)..],
            bitLength);

        // Process the final block(s)
        for (System.Int32 i = 0; i < blockCount; i++)
        {
            ProcessBlock(paddingBuffer.Slice(i * 64, 64), _state);
        }

        _finalized = true;

        // Convert hash to bytes in big-endian format
        unsafe
        {
            fixed (System.Byte* p = result)
            {
                System.UInt32* ptr = (System.UInt32*)p;
                for (System.Int32 i = 0; i < 5; i++)
                {
                    ptr[i] = System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(_state[i]);
                }
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
    /// <exception cref="System.ArgumentException">
    /// Thrown if the input data is excessively large (greater than 2^61 bytes),
    /// as SHA-1 uses a 64-bit length field.
    /// </exception>
    /// <exception cref="System.ObjectDisposedException">
    /// Thrown if this method is called after the object has been disposed.
    /// </exception>
    /// <remarks>
    /// This method follows the standard SHA-1 padding and processing rules:
    /// - The input data is processed in 64-byte blocks.
    /// - A padding byte `0x80` is added after the data.
    /// - The length of the original message (in bits) is appended in big-endian format.
    /// </remarks>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Byte[] ComputeHash(System.ReadOnlySpan<System.Byte> data)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(SHA1));

        // Reset the state to ensure independence from previous operations
        Initialize();

        // Create a temporary copy of the hash state to preserve the instance state
        System.Span<System.UInt32> h = stackalloc System.UInt32[5];

        unsafe
        {
            ref System.Byte srcRef = ref System.Runtime.CompilerServices.Unsafe.As<
                System.UInt32, System.Byte>(
                ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(_state));

            ref System.Byte dstRef = ref System.Runtime.CompilerServices.Unsafe.As<
                System.UInt32, System.Byte>(
                ref System.Runtime.InteropServices.MemoryMarshal.GetReference(h));

            System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(ref dstRef, ref srcRef, 20); // 5 * sizeof(uint)
        }

        // Calculate message length in bits (before padding)
        System.UInt64 bitLength = (System.UInt64)data.Length * 8;

        // Process all complete blocks
        System.Int32 fullBlocks = data.Length / 64;
        unsafe
        {
            ref System.Byte inputRef = ref System.Runtime.InteropServices.MemoryMarshal.GetReference(data);
            for (System.Int32 i = 0; i < fullBlocks; i++)
            {
                System.ReadOnlySpan<System.Byte> block = System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(
                    ref System.Runtime.CompilerServices.Unsafe.Add(ref inputRef, i * 64), 64);
                ProcessBlock(block, h);
            }
        }

        // Handle the final block with padding
        System.Int32 remainingBytes = data.Length % 64;
        System.Span<System.Byte> finalBlock = stackalloc System.Byte[128]; // Max 2 blocks needed

        // Copy remaining data to the final block
        if (remainingBytes > 0)
        {
            data[^remainingBytes..].CopyTo(finalBlock);
        }

        // Push the '1' bit
        finalBlock[remainingBytes] = 0x80;

        // Determine if we need one or two blocks
        System.Int32 blockCount = remainingBytes + 1 + 8 > 64 ? 2 : 1;
        System.Int32 finalBlockSize = blockCount * 64;

        // WriteInt16 the length in bits as a 64-bit big-endian integer
        unsafe
        {
            fixed (System.Byte* p = finalBlock)
            {
                *(System.UInt64*)(p + finalBlockSize - 8) = System.BitConverter.IsLittleEndian
                    ? System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(bitLength)
                    : bitLength;
            }
        }

        // Process the final block(s)
        for (System.Int32 i = 0; i < blockCount; i++)
        {
            ProcessBlock(finalBlock.Slice(i * 64, 64), h);
        }

        // Convert the hash to bytes in big-endian format
        unsafe
        {
            System.Byte[] result = new System.Byte[20];
            fixed (System.Byte* resultPtr = result)
            {
                System.UInt32 v0 = h[0];
                System.UInt32 v1 = h[1];
                System.UInt32 v2 = h[2];
                System.UInt32 v3 = h[3];
                System.UInt32 v4 = h[4];

                if (System.BitConverter.IsLittleEndian)
                {
                    v0 = System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(v0);
                    v1 = System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(v1);
                    v2 = System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(v2);
                    v3 = System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(v3);
                    v4 = System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(v4);
                }

                ((System.UInt32*)resultPtr)[0] = v0;
                ((System.UInt32*)resultPtr)[1] = v1;
                ((System.UInt32*)resultPtr)[2] = v2;
                ((System.UInt32*)resultPtr)[3] = v3;
                ((System.UInt32*)resultPtr)[4] = v4;

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

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static unsafe void ProcessBlock(
        System.ReadOnlySpan<System.Byte> block,
        System.Span<System.UInt32> h)
    {
        System.Span<System.UInt32> w = stackalloc System.UInt32[80];

        // Load first 16 words from big-endian data
        fixed (System.Byte* ptr = block)
        {
            for (System.Int32 j = 0; j < 16; j++)
            {
                w[j] = System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(
                    System.Runtime.CompilerServices.Unsafe.ReadUnaligned<System.UInt32>(ptr + (j << 2)));
            }
        }

        // Message schedule expansion
        for (System.Int32 j = 16; j < 80; j++)
        {
            w[j] = BitwiseOperations.RotateLeft(w[j - 3] ^ w[j - 8] ^ w[j - 14] ^ w[j - 16], 1);
        }

        // Initialize working variables
        System.UInt32 a = h[0], b = h[1], c = h[2], d = h[3], e = h[4];

        // Main loop - optimized with function inlining
        for (System.Int32 j = 0; j < 20; j++)
        {
            System.UInt32 temp = BitwiseOperations.RotateLeft(a, 5) + (b & c | ~b & d) + e + SHA.K1[0] + w[j];
            e = d;
            d = c;
            c = BitwiseOperations.RotateLeft(b, 30);
            b = a;
            a = temp;
        }

        for (System.Int32 j = 20; j < 40; j++)
        {
            System.UInt32 temp = BitwiseOperations.RotateLeft(a, 5) + (b ^ c ^ d) + e + SHA.K1[1] + w[j];
            e = d;
            d = c;
            c = BitwiseOperations.RotateLeft(b, 30);
            b = a;
            a = temp;
        }

        for (System.Int32 j = 40; j < 60; j++)
        {
            System.UInt32 temp = BitwiseOperations.RotateLeft(a, 5) + (b & c | b & d | c & d) + e + SHA.K1[2] + w[j];
            e = d;
            d = c;
            c = BitwiseOperations.RotateLeft(b, 30);
            b = a;
            a = temp;
        }

        for (System.Int32 j = 60; j < 80; j++)
        {
            System.UInt32 temp = BitwiseOperations.RotateLeft(a, 5) + (b ^ c ^ d) + e + SHA.K1[3] + w[j];
            e = d;
            d = c;
            c = BitwiseOperations.RotateLeft(b, 30);
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
    [System.Diagnostics.DebuggerNonUserCode]
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        // Clear sensitive data from memory
        System.Array.Clear(_state, 0, _state.Length);
        System.Array.Clear(_buffer, 0, _buffer.Length);

        _disposed = true;
        System.GC.SuppressFinalize(this);
    }

    #endregion IDisposable Implementation

    #region Overrides

    /// <summary>
    /// Returns a string representation of the SHA-1 hash algorithm.
    /// </summary>
    public override System.String ToString() => "SHA-1";

    #endregion Overrides
}