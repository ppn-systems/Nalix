// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Cryptography.Abstractions;

namespace Nalix.Cryptography.Hashing;

/// <summary>
/// Provides a managed implementation of the SHA-512 hashing algorithm.
/// </summary>
[System.Runtime.InteropServices.ComVisible(true)]
public sealed class SHA512 : IShaDigest, System.IDisposable
{
    #region Fields

    private readonly System.Byte[] _buffer = new System.Byte[128]; // 1024 bits = 128 bytes
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
    /// Initializes a new instance of the <see cref="SHA512"/> class.
    /// </summary>
    public SHA512() => Initialize();

    #endregion Constructors

    #region Public Methods

    /// <summary>
    /// Initializes or resets the hash state.
    /// </summary>
    [System.Diagnostics.DebuggerNonUserCode]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public void Initialize()
    {
        unsafe
        {
            ref System.Byte src = ref System.Runtime.CompilerServices.Unsafe.As<System.UInt64, System.Byte>(
                ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(SHA.H512));

            ref System.Byte dst = ref System.Runtime.CompilerServices.Unsafe.As<System.UInt64, System.Byte>(
                ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(_state));

            System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(
                ref dst, ref src, (System.UInt32)(SHA.H512.Length * sizeof(System.UInt64)));
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
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Byte[] HashData(System.ReadOnlySpan<System.Byte> data)
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
    /// <exception cref="System.ArgumentException">Thrown if the output span is less than 64 bytes.</exception>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void HashData(System.ReadOnlySpan<System.Byte> data, System.Span<System.Byte> output)
    {
        if (output.Length < 64)
        {
            throw new System.ArgumentException("Output must be at least 64 bytes.", nameof(output));
        }

        using SHA512 sha = new();
        sha.Update(data);
        System.MemoryExtensions.AsSpan(sha.FinalizeHash()).CopyTo(output);
    }

    /// <summary>
    /// Computes the SHA-512 hash for a single block of data.
    /// </summary>
    /// <param name="data">The input data.</param>
    /// <returns>The resulting hash.</returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Byte[] ComputeHash(System.ReadOnlySpan<System.Byte> data)
    {
        Update(data);
        return FinalizeHash();
    }

    /// <summary>
    /// Updates the hash state with the specified data.
    /// </summary>
    /// <param name="data">The data to hash.</param>
    /// <exception cref="System.ObjectDisposedException">Thrown if the object has been disposed.</exception>
    /// <exception cref="System.InvalidOperationException">Thrown if the hash has already been finalized.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Update(System.ReadOnlySpan<System.Byte> data)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(SHA512));
        if (_finalized)
        {
            throw new System.InvalidOperationException("Hash already finalized.");
        }

        // Update byte counters
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
                data.CopyTo(System.MemoryExtensions.AsSpan(_buffer, _bufferLength));
                _bufferLength += data.Length;
                return;
            }

            data[..toFill].CopyTo(System.MemoryExtensions.AsSpan(_buffer, _bufferLength));
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
    /// <exception cref="System.ObjectDisposedException">Thrown if the object has been disposed.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Byte[] FinalizeHash()
    {
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(SHA512));
        if (_finalized)
        {
            return (System.Byte[])_finalHash.Clone();
        }

        System.Span<System.Byte> padding = stackalloc System.Byte[256]; // overprovision
        padding.Clear();
        padding[0] = 0x80;

        System.Int32 padLength = (_bufferLength < 112) ? (112 - _bufferLength) : (240 - _bufferLength);
        Update(padding[..padLength]);

        System.Span<System.Byte> lengthBlock = stackalloc System.Byte[16];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(
            lengthBlock[..8], (_byteCountHigh << 3) | (_byteCountLow >> 61));
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(lengthBlock[8..], _byteCountLow << 3);
        Update(lengthBlock);

        System.Byte[] result = new System.Byte[64];
        for (System.Int32 i = 0; i < 8; i++)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(System.MemoryExtensions
                                                  .AsSpan(result, i * 8), _state[i]);
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private unsafe void ProcessBlock(System.ReadOnlySpan<System.Byte> block)
    {
        const System.Int32 rounds = 80;
        System.Span<System.UInt64> w = stackalloc System.UInt64[80];

        for (System.Int32 i = 0; i < 16; i++)
        {
            w[i] = System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(block.Slice(i * 8, 8));
        }

        for (System.Int32 i = 16; i < rounds; i++)
        {
            System.UInt64 s0 = System.Numerics.BitOperations.RotateRight(w[i - 15], 1) ^
                        System.Numerics.BitOperations.RotateRight(w[i - 15], 8) ^ (w[i - 15] >> 7);

            System.UInt64 s1 = System.Numerics.BitOperations.RotateRight(w[i - 2], 19) ^
                        System.Numerics.BitOperations.RotateRight(w[i - 2], 61) ^ (w[i - 2] >> 6);

            w[i] = unchecked(w[i - 16] + s0 + w[i - 7] + s1);
        }

        System.UInt64 a = _state[0];
        System.UInt64 b = _state[1];
        System.UInt64 c = _state[2];
        System.UInt64 d = _state[3];
        System.UInt64 e = _state[4];
        System.UInt64 f = _state[5];
        System.UInt64 g = _state[6];
        System.UInt64 h = _state[7];

        System.ReadOnlySpan<System.UInt64> K = SHA.K512;

        for (System.Int32 i = 0; i < rounds; i++)
        {
            System.UInt64 S1 = System.Numerics.BitOperations.RotateRight(e, 14) ^
                        System.Numerics.BitOperations.RotateRight(e, 18) ^
                        System.Numerics.BitOperations.RotateRight(e, 41);

            System.UInt64 ch = (e & f) ^ (~e & g);
            System.UInt64 temp1 = h + S1 + ch + K[i] + w[i];
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
    [System.Diagnostics.DebuggerNonUserCode]
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_finalHash != null)
        {
            System.Array.Clear(_finalHash, 0, _finalHash.Length);
        }

        System.Array.Clear(_state, 0, _state.Length);
    }

    #endregion IDisposable Implementation
}
