using Nalix.Common.Security.Cryptography.Hashing;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Nalix.Cryptography.Hashing;

/// <summary>
/// Provides a managed implementation of the SHA-512 hashing algorithm.
/// </summary>
[System.Runtime.InteropServices.ComVisible(true)]
public sealed class SHA512 : IShaDigest, IDisposable
{
    #region Fields

    private readonly Byte[] _buffer = new Byte[128]; // 1024 bits = 128 bytes
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
    /// Initializes a new instance of the <see cref="SHA512"/> class.
    /// </summary>
    public SHA512() => Initialize();

    #endregion Constructors

    #region Public Methods

    /// <summary>
    /// Initializes or resets the hash state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Initialize()
    {
        unsafe
        {
            ref Byte src = ref System.Runtime.CompilerServices.Unsafe.As<UInt64, Byte>(
                ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(SHA.H512));

            ref Byte dst = ref System.Runtime.CompilerServices.Unsafe.As<UInt64, Byte>(
                ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(_state));

            System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(
                ref dst, ref src, (UInt32)(SHA.H512.Length * sizeof(UInt64)));
        }

        _bufferLength = 0;
        _byteCountLow = 0;
        _byteCountHigh = 0;
        _finalized = false;
        _disposed = false;
        _finalHash = null;
    }

    /// <summary>
    /// Computes the SHA-512 hash of the specified data.
    /// </summary>
    /// <param name="data">The input data to hash.</param>
    /// <returns>The computed SHA-512 hash as a byte array.</returns>
    public static Byte[] HashData(ReadOnlySpan<Byte> data)
    {
        using SHA512 sha = new();
        sha.Update(data);
        return sha.FinalizeHash();
    }

    /// <summary>
    /// Computes the SHA-512 hash of the specified data and writes it to the output span.
    /// </summary>
    /// <param name="data">The input data to hash.</param>
    /// <param name="output">The span to receive the 64-byte hash output.</param>
    /// <exception cref="ArgumentException">Thrown if the output span is less than 64 bytes.</exception>
    public static void HashData(ReadOnlySpan<Byte> data, Span<Byte> output)
    {
        if (output.Length < 64)
        {
            throw new ArgumentException("Output must be at least 64 bytes.", nameof(output));
        }

        using SHA512 sha = new();
        sha.Update(data);
        sha.FinalizeHash().AsSpan().CopyTo(output);
    }

    /// <summary>
    /// Computes the SHA-512 hash for a single block of data.
    /// </summary>
    /// <param name="data">The input data.</param>
    /// <returns>The resulting hash.</returns>
    public Byte[] ComputeHash(ReadOnlySpan<Byte> data)
    {
        Update(data);
        return FinalizeHash();
    }

    /// <summary>
    /// Updates the hash state with the specified data.
    /// </summary>
    /// <param name="data">The data to hash.</param>
    /// <exception cref="ObjectDisposedException">Thrown if the object has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the hash has already been finalized.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update(ReadOnlySpan<Byte> data)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(SHA512));
        if (_finalized)
        {
            throw new InvalidOperationException("Hash already finalized.");
        }

        // Update byte counters
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
                data.CopyTo(_buffer.AsSpan(_bufferLength));
                _bufferLength += data.Length;
                return;
            }

            data[..toFill].CopyTo(_buffer.AsSpan(_bufferLength));
            ProcessBlock(_buffer);
            data = data[toFill..];
        }

        while (data.Length >= 128)
        {
            ProcessBlock(data[..128]);
            data = data[128..];
        }

        data.CopyTo(_buffer);
        _bufferLength = data.Length;
    }

    /// <summary>
    /// Finalizes the hash computation and returns the resulting SHA-512 hash.
    /// </summary>
    /// <returns>The 64-byte hash.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the object has been disposed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Byte[] FinalizeHash()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(SHA512));
        if (_finalized)
        {
            return (Byte[])_finalHash.Clone();
        }

        Span<Byte> padding = stackalloc Byte[256]; // overprovision
        padding.Clear();
        padding[0] = 0x80;

        Int32 padLength = (_bufferLength < 112) ? (112 - _bufferLength) : (240 - _bufferLength);
        Update(padding[..padLength]);

        Span<Byte> lengthBlock = stackalloc Byte[16];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(
            lengthBlock[..8], (_byteCountHigh << 3) | (_byteCountLow >> 61));
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(lengthBlock[8..], _byteCountLow << 3);
        Update(lengthBlock);

        Byte[] result = new Byte[64];
        for (Int32 i = 0; i < 8; i++)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(result.AsSpan(i * 8), _state[i]);
        }

        _finalHash = result;
        _finalized = true;
        return result;
    }

    #endregion Public Methods

    #region Private Methods

    /// <summary>
    /// Processes a 128-byte (1024-bit) block of the message.
    /// </summary>
    /// <param name="block">The 128-byte block to process.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void ProcessBlock(ReadOnlySpan<Byte> block)
    {
        const Int32 rounds = 80;
        Span<UInt64> w = stackalloc UInt64[80];

        for (Int32 i = 0; i < 16; i++)
        {
            w[i] = System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(block.Slice(i * 8, 8));
        }

        for (Int32 i = 16; i < rounds; i++)
        {
            UInt64 s0 = BitOperations.RotateRight(w[i - 15], 1) ^ BitOperations.RotateRight(w[i - 15], 8) ^ (w[i - 15] >> 7);
            UInt64 s1 = BitOperations.RotateRight(w[i - 2], 19) ^ BitOperations.RotateRight(w[i - 2], 61) ^ (w[i - 2] >> 6);
            w[i] = unchecked(w[i - 16] + s0 + w[i - 7] + s1);
        }

        UInt64 a = _state[0];
        UInt64 b = _state[1];
        UInt64 c = _state[2];
        UInt64 d = _state[3];
        UInt64 e = _state[4];
        UInt64 f = _state[5];
        UInt64 g = _state[6];
        UInt64 h = _state[7];

        ReadOnlySpan<UInt64> K = SHA.K512;

        for (Int32 i = 0; i < rounds; i++)
        {
            UInt64 S1 = BitOperations.RotateRight(e, 14) ^ BitOperations.RotateRight(e, 18) ^ BitOperations.RotateRight(e, 41);
            UInt64 ch = (e & f) ^ (~e & g);
            UInt64 temp1 = h + S1 + ch + K[i] + w[i];
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

        _state[0] += a;
        _state[1] += b;
        _state[2] += c;
        _state[3] += d;
        _state[4] += e;
        _state[5] += f;
        _state[6] += g;
        _state[7] += h;
    }

    #endregion Private Methods

    #region IDisposable Implementation

    /// <summary>
    /// Releases all resources used by the <see cref="SHA512"/> instance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_finalHash != null)
        {
            Array.Clear(_finalHash, 0, _finalHash.Length);
        }

        Array.Clear(_state, 0, _state.Length);
    }

    #endregion IDisposable Implementation
}
