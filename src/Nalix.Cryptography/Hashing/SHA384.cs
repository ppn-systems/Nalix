using Nalix.Common.Security.Cryptography.Hashing;

namespace Nalix.Cryptography.Hashing;

/// <summary>
/// Provides a high-performance unsafe implementation of the SHA-384 hashing algorithm,
/// compliant with FIPS PUB 180-4.
/// </summary>
/// <remarks>
/// SHA-384 is a truncated version of SHA-512. This implementation uses only the
/// first 384 bits (48 bytes) of the final 512-bit internal state.
/// <para>
/// It is optimized for maximum performance using direct memory manipulation,
/// and is intended for use in secure cryptographic applications where
/// .NET's built-in algorithms are not desired or available.
/// </para>
/// <para>
/// This implementation is not thread-safe. Create separate instances for concurrent use.
/// </para>
/// </remarks>
[System.Runtime.InteropServices.ComVisible(true)]
public sealed class SHA384 : IShaDigest, System.IDisposable
{
    #region Fields

    private readonly System.Byte[] _buffer = new System.Byte[128];
    private readonly System.UInt64[] _state = new System.UInt64[8];

    private System.UInt64 _byteCountHigh;
    private System.UInt64 _byteCountLow;
    private System.Byte[] _finalHash;
    private System.Int32 _bufferLength;
    private System.Boolean _finalized;
    private System.Boolean _disposed;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="SHA384"/> class.
    /// </summary>
    public SHA384() => Initialize();

    #endregion Constructors

    #region Public Methods

    /// <summary>
    /// Computes the SHA-384 hash for the provided input data.
    /// </summary>
    /// <param name="data">The input data to hash.</param>
    /// <returns>A 48-byte array containing the SHA-384 hash.</returns>
    public static System.Byte[] HashData(System.ReadOnlySpan<System.Byte> data)
    {
        using SHA384 sha = new();
        sha.Update(data);
        return sha.FinalizeHash();
    }

    /// <summary>
    /// Computes the SHA-384 hash for the provided input data and writes it to the output span.
    /// </summary>
    /// <param name="data">The input data to hash.</param>
    /// <param name="output">The span to receive the 48-byte hash.</param>
    /// <exception cref="System.ArgumentException">Thrown if <paramref name="output"/> is less than 48 bytes.</exception>
    public static void HashData(System.ReadOnlySpan<System.Byte> data, System.Span<System.Byte> output)
    {
        if (output.Length < 48)
        {
            throw new System.ArgumentException("Output must be at least 48 bytes.", nameof(output));
        }

        using SHA384 sha = new();
        sha.Update(data);

        // Use direct pointer access for faster copying
        unsafe
        {
            fixed (System.Byte* resultPtr = sha.FinalizeHash())
            fixed (System.Byte* outputPtr = output)
            {
                System.Buffer.MemoryCopy(resultPtr, outputPtr, 48, 48);
            }
        }
    }

    /// <summary>
    /// Initializes the <see cref="SHA384"/> instance to its initial state.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Initialize()
    {
        unsafe
        {
            // Get ref to first byte of SHA.H384 and _state
            ref System.Byte src = ref System.Runtime.CompilerServices.Unsafe.As<System.UInt64, System.Byte>(
                ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(SHA.H384));

            ref System.Byte dst = ref System.Runtime.CompilerServices.Unsafe.As<System.UInt64, System.Byte>(
                ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(_state));

            // Copy entire block of memory (as raw bytes)
            System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(
                ref dst, ref src, (System.UInt32)(SHA.H384.Length * sizeof(System.UInt64)));
        }

        _bufferLength = 0;
        _byteCountLow = 0;
        _byteCountHigh = 0;
        _finalized = false;
        _disposed = false;
        _finalHash = null;
    }

    /// <summary>
    /// Updates the hash computation with the provided data.
    /// </summary>
    /// <param name="data">The input data to include in the hash.</param>
    /// <exception cref="System.ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    /// <exception cref="System.InvalidOperationException">Thrown if the hash has already been finalized.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Update(System.ReadOnlySpan<System.Byte> data)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(SHA384));
        if (_finalized)
        {
            throw new System.InvalidOperationException("Hash already finalized.");
        }

        System.UInt64 bits = (System.UInt64)data.Length;
        _byteCountLow += bits;
        if (_byteCountLow < bits)
        {
            _byteCountHigh++;
        }

        if (_bufferLength > 0)
        {
            System.Int32 toFill = 128 - _bufferLength;
            if (data.Length < toFill)
            {
                // Use unsafe code for small copies
                unsafe
                {
                    fixed (System.Byte* destPtr = &_buffer[_bufferLength])
                    fixed (System.Byte* srcPtr = data)
                    {
                        System.Buffer.MemoryCopy(srcPtr, destPtr, data.Length, data.Length);
                    }
                }
                _bufferLength += data.Length;
                return;
            }

            // Fill buffer and process it
            unsafe
            {
                fixed (System.Byte* destPtr = &_buffer[_bufferLength])
                fixed (System.Byte* srcPtr = data)
                {
                    System.Buffer.MemoryCopy(srcPtr, destPtr, toFill, toFill);
                }
            }

            ProcessBlock(_buffer);
            data = data[toFill..];
        }

        // Process full blocks directly from input data
        unsafe
        {
            fixed (System.Byte* dataPtr = data)
            {
                System.Byte* currentPtr = dataPtr;
                System.Int32 remainingLength = data.Length;

                // Process full blocks with pointer arithmetic (faster than span slicing)
                while (remainingLength >= 128)
                {
                    ProcessBlockDirect(currentPtr);
                    currentPtr += 128;
                    remainingLength -= 128;
                }

                // Copy any remaining bytes to the buffer
                if (remainingLength > 0)
                {
                    fixed (System.Byte* bufferPtr = _buffer)
                    {
                        System.Buffer.MemoryCopy(currentPtr, bufferPtr, remainingLength, remainingLength);
                    }
                    _bufferLength = remainingLength;
                }
            }
        }
    }

    /// <summary>
    /// Finalizes the hash computation and returns the SHA-384 hash.
    /// </summary>
    /// <returns>A 48-byte array containing the SHA-384 hash.</returns>
    /// <exception cref="System.ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public unsafe System.Byte[] FinalizeHash()
    {
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(SHA384));
        if (_finalized)
        {
            return (System.Byte[])_finalHash.Clone();
        }

        // Prepare padding
        System.Byte* paddingPtr = stackalloc System.Byte[256];
        // Clear padding array
        for (System.Int32 i = 0; i < 256; i++)
        {
            paddingPtr[i] = 0;
        }

        paddingPtr[0] = 0x80;

        System.Int32 padLength = (_bufferLength < 112) ? (112 - _bufferLength) : (240 - _bufferLength);

        // Create a span from the padding buffer and update
        System.Span<System.Byte> paddingSpan = new(paddingPtr, 256);
        Update(paddingSpan[..padLength]);

        // Prepare length block
        System.Byte* lengthBlockPtr = stackalloc System.Byte[16];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(
            new System.Span<System.Byte>(lengthBlockPtr, 8), (_byteCountHigh << 3) | (_byteCountLow >> 61));
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(
            new System.Span<System.Byte>(lengthBlockPtr + 8, 8), _byteCountLow << 3);

        Update(new System.Span<System.Byte>(lengthBlockPtr, 16));

        // Create result
        System.Byte[] result = new System.Byte[48]; // Only 6 state words used
        fixed (System.Byte* resultPtr = result)
        {
            System.UInt64* resultULongPtr = (System.UInt64*)resultPtr;

            for (System.Int32 i = 0; i < 6; i++)
            {
                // WriteInt16 the state values in big endian format
                System.UInt64 value = _state[i];
                System.Byte* valuePtr = (System.Byte*)(resultULongPtr + i);

                valuePtr[0] = (System.Byte)(value >> 56);
                valuePtr[1] = (System.Byte)(value >> 48);
                valuePtr[2] = (System.Byte)(value >> 40);
                valuePtr[3] = (System.Byte)(value >> 32);
                valuePtr[4] = (System.Byte)(value >> 24);
                valuePtr[5] = (System.Byte)(value >> 16);
                valuePtr[6] = (System.Byte)(value >> 8);
                valuePtr[7] = (System.Byte)value;
            }
        }

        _finalHash = result;
        _finalized = true;
        return result;
    }

    /// <summary>
    /// Updates the hash with the provided data and finalizes the computation.
    /// </summary>
    /// <param name="data">The input data to include in the hash.</param>
    /// <returns>A 48-byte array containing the SHA-384 hash.</returns>
    /// <exception cref="System.ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    public System.Byte[] ComputeHash(System.ReadOnlySpan<System.Byte> data)
    {
        Update(data);
        return FinalizeHash();
    }

    #endregion Public Methods

    #region Private Methods

    /// <summary>
    /// Processes a 128-byte block of data as part of the SHA-384 hash computation.
    /// </summary>
    /// <param name="block">The 128-byte block to process.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private unsafe void ProcessBlock(System.ReadOnlySpan<System.Byte> block)
    {
        fixed (System.Byte* blockPtr = block)
        {
            ProcessBlockDirect(blockPtr);
        }
    }

    /// <summary>
    /// Processes a 128-byte block of data directly from a memory pointer.
    /// </summary>
    /// <param name="blockPtr">Pointer to the 128-byte block to process.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private unsafe void ProcessBlockDirect(System.Byte* blockPtr)
    {
        const System.Int32 rounds = 80;
        System.UInt64* w = stackalloc System.UInt64[rounds];

        // Convert block from big-endian bytes to native ulong
        for (System.Int32 i = 0; i < 16; i++)
        {
            System.Int32 offset = i * 8;
            w[i] = ((System.UInt64)blockPtr[offset] << 56) |
                   ((System.UInt64)blockPtr[offset + 1] << 48) |
                   ((System.UInt64)blockPtr[offset + 2] << 40) |
                   ((System.UInt64)blockPtr[offset + 3] << 32) |
                   ((System.UInt64)blockPtr[offset + 4] << 24) |
                   ((System.UInt64)blockPtr[offset + 5] << 16) |
                   ((System.UInt64)blockPtr[offset + 6] << 8) |
                    blockPtr[offset + 7];
        }

        // Message schedule (phương trình lập lịch thông điệp)
        for (System.Int32 i = 16; i < rounds; i++)
        {
            System.UInt64 s0 = System.Numerics.BitOperations.RotateRight(w[i - 15], 1) ^
                        System.Numerics.BitOperations.RotateRight(w[i - 15], 8) ^ (w[i - 15] >> 7);

            System.UInt64 s1 = System.Numerics.BitOperations.RotateRight(w[i - 2], 19) ^
                        System.Numerics.BitOperations.RotateRight(w[i - 2], 61) ^ (w[i - 2] >> 6);

            w[i] = w[i - 16] + s0 + w[i - 7] + s1;
        }

        // Initialize working variables
        System.UInt64 a = _state[0], b = _state[1], c = _state[2], d = _state[3];
        System.UInt64 e = _state[4], f = _state[5], g = _state[6], h = _state[7];

        fixed (System.UInt64* kPtr = SHA.K512)
        {
            // Main loop
            for (System.Int32 i = 0; i < rounds; i++)
            {
                System.UInt64 S1 = System.Numerics.BitOperations.RotateRight(e, 14) ^
                            System.Numerics.BitOperations.RotateRight(e, 18) ^
                            System.Numerics.BitOperations.RotateRight(e, 41);

                System.UInt64 ch = (e & f) ^ (~e & g);
                System.UInt64 temp1 = h + S1 + ch + kPtr[i] + w[i];
                System.UInt64 S0 = System.Numerics.BitOperations.RotateRight(a, 28) ^
                            System.Numerics.BitOperations.RotateRight(a, 34) ^
                            System.Numerics.BitOperations.RotateRight(a, 39);

                System.UInt64 maj = (a & b) ^ (a & c) ^ (b & c);
                System.UInt64 temp2 = S0 + maj;

                h = g;
                g = f;
                f = e;
                e = d + temp1;
                d = c;
                c = b;
                b = a;
                a = temp1 + temp2;
            }
        }

        // Update state
        _state[0] += a; _state[1] += b; _state[2] += c; _state[3] += d;
        _state[4] += e; _state[5] += f; _state[6] += g; _state[7] += h;
    }

    #endregion Private Methods

    #region IDisposable

    /// <summary>
    /// Disposes the <see cref="SHA384"/> instance, clearing sensitive data.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public unsafe void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Securely clear memory containing sensitive cryptographic data
        if (_finalHash != null)
        {
            fixed (System.Byte* hashPtr = _finalHash)
            {
                for (System.Int32 i = 0; i < _finalHash.Length; i++)
                {
                    hashPtr[i] = 0;
                }
            }
        }

        fixed (System.UInt64* statePtr = _state)
        {
            for (System.Int32 i = 0; i < _state.Length; i++)
            {
                statePtr[i] = 0;
            }
        }
    }

    #endregion IDisposable
}
