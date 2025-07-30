using Nalix.Common.Security.Cryptography.Hashing;
using System;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;

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
public sealed class SHA384 : IShaDigest, IDisposable
{
    #region Fields

    private readonly Byte[] _buffer = new Byte[128];
    private readonly UInt64[] _state = new UInt64[8];

    private UInt64 _byteCountHigh;
    private UInt64 _byteCountLow;
    private Byte[] _finalHash;
    private Int32 _bufferLength;
    private Boolean _finalized;
    private Boolean _disposed;

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
    public static Byte[] HashData(ReadOnlySpan<Byte> data)
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
    /// <exception cref="ArgumentException">Thrown if <paramref name="output"/> is less than 48 bytes.</exception>
    public static void HashData(ReadOnlySpan<Byte> data, Span<Byte> output)
    {
        if (output.Length < 48)
        {
            throw new ArgumentException("Output must be at least 48 bytes.", nameof(output));
        }

        using SHA384 sha = new();
        sha.Update(data);

        // Use direct pointer access for faster copying
        unsafe
        {
            fixed (Byte* resultPtr = sha.FinalizeHash())
            fixed (Byte* outputPtr = output)
            {
                Buffer.MemoryCopy(resultPtr, outputPtr, 48, 48);
            }
        }
    }

    /// <summary>
    /// Initializes the <see cref="SHA384"/> instance to its initial state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Initialize()
    {
        unsafe
        {
            // Get ref to first byte of SHA.H384 and _state
            ref Byte src = ref Unsafe.As<UInt64, Byte>(
                ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(SHA.H384));

            ref Byte dst = ref Unsafe.As<UInt64, Byte>(
                ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(_state));

            // Copy entire block of memory (as raw bytes)
            Unsafe.CopyBlockUnaligned(ref dst, ref src, (UInt32)(SHA.H384.Length * sizeof(UInt64)));
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
    /// <exception cref="ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the hash has already been finalized.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update(ReadOnlySpan<Byte> data)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(SHA384));
        if (_finalized)
        {
            throw new InvalidOperationException("Hash already finalized.");
        }

        UInt64 bits = (UInt64)data.Length;
        _byteCountLow += bits;
        if (_byteCountLow < bits)
        {
            _byteCountHigh++;
        }

        if (_bufferLength > 0)
        {
            Int32 toFill = 128 - _bufferLength;
            if (data.Length < toFill)
            {
                // Use unsafe code for small copies
                unsafe
                {
                    fixed (Byte* destPtr = &_buffer[_bufferLength])
                    fixed (Byte* srcPtr = data)
                    {
                        Buffer.MemoryCopy(srcPtr, destPtr, data.Length, data.Length);
                    }
                }
                _bufferLength += data.Length;
                return;
            }

            // Fill buffer and process it
            unsafe
            {
                fixed (Byte* destPtr = &_buffer[_bufferLength])
                fixed (Byte* srcPtr = data)
                {
                    Buffer.MemoryCopy(srcPtr, destPtr, toFill, toFill);
                }
            }
            ProcessBlock(_buffer);
            data = data[toFill..];
        }

        // Process full blocks directly from input data
        unsafe
        {
            fixed (Byte* dataPtr = data)
            {
                Byte* currentPtr = dataPtr;
                Int32 remainingLength = data.Length;

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
                    fixed (Byte* bufferPtr = _buffer)
                    {
                        Buffer.MemoryCopy(currentPtr, bufferPtr, remainingLength, remainingLength);
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
    /// <exception cref="ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe Byte[] FinalizeHash()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(SHA384));
        if (_finalized)
        {
            return (Byte[])_finalHash.Clone();
        }

        // Prepare padding
        Byte* paddingPtr = stackalloc Byte[256];
        // Clear padding array
        for (Int32 i = 0; i < 256; i++)
        {
            paddingPtr[i] = 0;
        }

        paddingPtr[0] = 0x80;

        Int32 padLength = (_bufferLength < 112) ? (112 - _bufferLength) : (240 - _bufferLength);

        // Create a span from the padding buffer and update
        Span<Byte> paddingSpan = new(paddingPtr, 256);
        Update(paddingSpan[..padLength]);

        // Prepare length block
        Byte* lengthBlockPtr = stackalloc Byte[16];
        BinaryPrimitives.WriteUInt64BigEndian(new Span<Byte>(lengthBlockPtr, 8), (_byteCountHigh << 3) | (_byteCountLow >> 61));
        BinaryPrimitives.WriteUInt64BigEndian(new Span<Byte>(lengthBlockPtr + 8, 8), _byteCountLow << 3);
        Update(new Span<Byte>(lengthBlockPtr, 16));

        // Create result
        Byte[] result = new Byte[48]; // Only 6 state words used
        fixed (Byte* resultPtr = result)
        {
            UInt64* resultULongPtr = (UInt64*)resultPtr;

            for (Int32 i = 0; i < 6; i++)
            {
                // WriteInt16 the state values in big endian format
                UInt64 value = _state[i];
                Byte* valuePtr = (Byte*)(resultULongPtr + i);

                valuePtr[0] = (Byte)(value >> 56);
                valuePtr[1] = (Byte)(value >> 48);
                valuePtr[2] = (Byte)(value >> 40);
                valuePtr[3] = (Byte)(value >> 32);
                valuePtr[4] = (Byte)(value >> 24);
                valuePtr[5] = (Byte)(value >> 16);
                valuePtr[6] = (Byte)(value >> 8);
                valuePtr[7] = (Byte)value;
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
    /// <exception cref="ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    public Byte[] ComputeHash(ReadOnlySpan<Byte> data)
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void ProcessBlock(ReadOnlySpan<Byte> block)
    {
        fixed (Byte* blockPtr = block)
        {
            ProcessBlockDirect(blockPtr);
        }
    }

    /// <summary>
    /// Processes a 128-byte block of data directly from a memory pointer.
    /// </summary>
    /// <param name="blockPtr">Pointer to the 128-byte block to process.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void ProcessBlockDirect(Byte* blockPtr)
    {
        const Int32 rounds = 80;
        UInt64* w = stackalloc UInt64[rounds];

        // Convert block from big-endian bytes to native ulong
        for (Int32 i = 0; i < 16; i++)
        {
            Int32 offset = i * 8;
            w[i] = ((UInt64)blockPtr[offset] << 56) |
                   ((UInt64)blockPtr[offset + 1] << 48) |
                   ((UInt64)blockPtr[offset + 2] << 40) |
                   ((UInt64)blockPtr[offset + 3] << 32) |
                   ((UInt64)blockPtr[offset + 4] << 24) |
                   ((UInt64)blockPtr[offset + 5] << 16) |
                   ((UInt64)blockPtr[offset + 6] << 8) |
                    blockPtr[offset + 7];
        }

        // Message schedule (phương trình lập lịch thông điệp)
        for (Int32 i = 16; i < rounds; i++)
        {
            UInt64 s0 = BitOperations.RotateRight(w[i - 15], 1) ^ BitOperations.RotateRight(w[i - 15], 8) ^ (w[i - 15] >> 7);
            UInt64 s1 = BitOperations.RotateRight(w[i - 2], 19) ^ BitOperations.RotateRight(w[i - 2], 61) ^ (w[i - 2] >> 6);
            w[i] = w[i - 16] + s0 + w[i - 7] + s1;
        }

        // Initialize working variables
        UInt64 a = _state[0], b = _state[1], c = _state[2], d = _state[3];
        UInt64 e = _state[4], f = _state[5], g = _state[6], h = _state[7];

        fixed (UInt64* kPtr = SHA.K512)
        {
            // Main loop
            for (Int32 i = 0; i < rounds; i++)
            {
                UInt64 S1 = BitOperations.RotateRight(e, 14) ^ BitOperations.RotateRight(e, 18) ^ BitOperations.RotateRight(e, 41);
                UInt64 ch = (e & f) ^ (~e & g);
                UInt64 temp1 = h + S1 + ch + kPtr[i] + w[i];
                UInt64 S0 = BitOperations.RotateRight(a, 28) ^ BitOperations.RotateRight(a, 34) ^ BitOperations.RotateRight(a, 39);
                UInt64 maj = (a & b) ^ (a & c) ^ (b & c);
                UInt64 temp2 = S0 + maj;

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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            fixed (Byte* hashPtr = _finalHash)
            {
                for (Int32 i = 0; i < _finalHash.Length; i++)
                {
                    hashPtr[i] = 0;
                }
            }
        }

        fixed (UInt64* statePtr = _state)
        {
            for (Int32 i = 0; i < _state.Length; i++)
            {
                statePtr[i] = 0;
            }
        }
    }

    #endregion IDisposable
}
