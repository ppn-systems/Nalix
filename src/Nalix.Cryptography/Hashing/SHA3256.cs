// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Cryptography.Abstractions;

namespace Nalix.Cryptography.Hashing;

/// <summary>
/// SHA3-256 (FIPS 202) implementation based on Keccak-f[1600] sponge construction.
/// Supports incremental updates and streaming; produces a 32-byte (256-bit) digest.
/// </summary>
/// <remarks>
/// Parameters for SHA3-256:
/// - State width: 1600 bits (25 lanes of 64 bits)
/// - Rate: 1088 bits (136 bytes)
/// - Capacity: 512 bits
/// - Padding: 0x06 domain separation with final 0x80 bit
/// </remarks>
[System.Runtime.InteropServices.ComVisible(true)]
[System.Diagnostics.DebuggerDisplay("Disposed={_disposed}, Finalized={_finalized}, Bytes={_byteCount}")]
public sealed class SHA3256 : IShaDigest, System.IDisposable
{
    #region Constants

    // Keccak-f[1600] parameters
    private const System.Byte KeccakRounds = 24;
    private const System.Byte RateBytes = 136; // 1088-bit rate for SHA3-256
    private const System.Byte HashSizeBytes = 32;

    #endregion

    #region Fields

    // 5x5 lanes of 64-bit (little-endian lanes)
    private readonly System.UInt64[] _state = new System.UInt64[25];

    // Absorb buffer of 'rate' bytes
    private readonly System.Byte[] _buffer = new System.Byte[RateBytes];
    private System.Int32 _bufferLen;

    private System.Boolean _finalized;
    private System.Boolean _disposed;
    private System.UInt64 _byteCount;
    private System.Byte[] _finalHash;

    #endregion

    #region Constructors

    /// <summary>Initializes a new instance of the <see cref="SHA3256"/> class.</summary>
    public SHA3256() => Initialize();

    #endregion

    #region Public API

    /// <summary>Resets the hash state.</summary>
    [System.Diagnostics.DebuggerNonUserCode]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public void Initialize()
    {
        System.Array.Clear(_state, 0, _state.Length);
        _bufferLen = 0;
        _finalized = false;
        _disposed = false;
        _byteCount = 0;

        if (_finalHash != null)
        {
            System.Array.Clear(_finalHash, 0, _finalHash.Length);
            _finalHash = null;
        }
    }

    /// <summary>
    /// Computes SHA3-256 for the provided data and returns a new 32-byte array.
    /// </summary>
    [System.Diagnostics.Contracts.Pure]
    public static System.Byte[] HashData(System.ReadOnlySpan<System.Byte> data)
    {
        using var sha3 = new SHA3256();
        sha3.Update(data);
        return sha3.FinalizeHash();
    }

    /// <summary>
    /// Computes SHA3-256 for the provided data and writes to the given output span (at least 32 bytes).
    /// </summary>
    [System.Diagnostics.Contracts.Pure]
    public static void HashData(System.ReadOnlySpan<System.Byte> data, System.Span<System.Byte> output)
    {
        if (output.Length < HashSizeBytes)
        {
            throw new System.ArgumentException("Output buffer must be at least 32 bytes.", nameof(output));
        }

        using var sha3 = new SHA3256();
        sha3.Update(data);
        sha3.FinalizeHash(output);
    }

    /// <summary>
    /// Incrementally updates the hasher with <paramref name="data"/>.
    /// </summary>
    /// <exception cref="System.ObjectDisposedException"/>
    /// <exception cref="System.InvalidOperationException"/>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Update(System.ReadOnlySpan<System.Byte> data)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(SHA3256));
        if (_finalized)
        {
            throw new System.InvalidOperationException("Cannot update after finalization.");
        }

        var input = data;

        // If buffer holds partial block, fill it first
        if (_bufferLen > 0)
        {
            System.Int32 toFill = RateBytes - _bufferLen;
            if (toFill > input.Length)
            {
                toFill = input.Length;
            }

            input[..toFill].CopyTo(System.MemoryExtensions.AsSpan(_buffer, _bufferLen));
            _bufferLen += toFill;
            input = input[toFill..];

            if (_bufferLen == RateBytes)
            {
                AbsorbBlock(_buffer);
                _bufferLen = 0;
            }
        }

        // Process full rate blocks directly from input
        while (input.Length >= RateBytes)
        {
            AbsorbBlock(input[..RateBytes]);
            input = input[RateBytes..];
        }

        // Buffer tail
        if (!input.IsEmpty)
        {
            input.CopyTo(System.MemoryExtensions.AsSpan(_buffer, _bufferLen));
            _bufferLen += input.Length;
        }

        _byteCount += (System.UInt64)data.Length;
    }

    /// <summary>
    /// Finalizes the hash and returns a new 32-byte array with the digest.
    /// </summary>
    /// <exception cref="System.ObjectDisposedException"/>
    [System.Diagnostics.Contracts.Pure]
    public System.Byte[] FinalizeHash()
    {
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(SHA3256));
        if (_finalized && _finalHash != null)
        {
            return (System.Byte[])_finalHash.Clone();
        }

        ApplyPaddingAndAbsorb();

        // Squeeze 32 bytes
        var result = new System.Byte[HashSizeBytes];
        Squeeze(result);
        _finalHash = result;
        _finalized = true;
        return (System.Byte[])_finalHash.Clone();
    }

    /// <summary>
    /// Finalizes the hash and writes 32 bytes to <paramref name="output"/>.
    /// </summary>
    /// <exception cref="System.ArgumentException"/>
    /// <exception cref="System.ObjectDisposedException"/>
    [System.Diagnostics.Contracts.Pure]
    public void FinalizeHash(System.Span<System.Byte> output)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(SHA3256));
        if (output.Length < HashSizeBytes)
        {
            throw new System.ArgumentException("Output buffer must be at least 32 bytes.", nameof(output));
        }

        if (_finalized && _finalHash != null)
        {
            System.MemoryExtensions.AsSpan(_finalHash).CopyTo(output);
            return;
        }

        ApplyPaddingAndAbsorb();

        // Squeeze 32 bytes
        System.Span<System.Byte> tmp = stackalloc System.Byte[HashSizeBytes];
        Squeeze(tmp);
        tmp.CopyTo(output);

        _finalHash = tmp.ToArray();
        _finalized = true;
    }

    /// <inheritdoc/>
    public System.Byte[] ComputeHash(System.ReadOnlySpan<System.Byte> data)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(SHA256));

        // Process the data
        Update(data);
        System.Byte[] result = System.GC.AllocateUninitializedArray<System.Byte>(32);
        FinalizeHash(result);
        return result;
    }

    #endregion

    #region IDisposable

    /// <inheritdoc/>
    [System.Diagnostics.DebuggerNonUserCode]
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        System.Array.Clear(_buffer, 0, _buffer.Length);
        System.Array.Clear(_state, 0, _state.Length);
        if (_finalHash != null)
        {
            System.Array.Clear(_finalHash, 0, _finalHash.Length);
        }

        _disposed = true;
        System.GC.SuppressFinalize(this);
    }

    #endregion

    #region Keccak Core

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void AbsorbBlock(System.ReadOnlySpan<System.Byte> block)
    {
        // XOR block (rate portion) into state as 64-bit little-endian lanes
        // RateBytes is multiple of 8 (136), so safe to step by 8
        var s = _state;
        System.Int32 lane = 0;
        for (System.Int32 i = 0; i < RateBytes; i += 8, lane++)
        {
            System.UInt64 v = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(i, 8));
            s[lane] ^= v;
        }

        KeccakF1600(s);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void ApplyPaddingAndAbsorb()
    {
        // Domain padding 0x06 ... 0x80 (multi-rate padding for SHA-3)
        // Add 0x06 at current buffer position, pad zeros, set MSB of last byte (0x80)
        var pad = _buffer;
        System.Int32 n = _bufferLen;

        // If there's no room for final 0x80 in this block, we need two blocks.
        if (n == RateBytes)
        {
            // Unlikely, but if exactly full, process it first.
            AbsorbBlock(pad);
            n = 0;
            _bufferLen = 0;
        }

        pad[n++] = 0x06; // domain separation for SHA-3
        if (n < RateBytes)
        {
            System.Array.Clear(pad, n, RateBytes - n);
            pad[RateBytes - 1] |= 0x80;
            AbsorbBlock(pad);
        }
        else
        {
            // n == RateBytes; we used last byte with 0x06 and need another full block with 0x80 at end
            AbsorbBlock(pad);
            System.Array.Clear(pad, 0, RateBytes);
            pad[RateBytes - 1] = 0x80;
            AbsorbBlock(pad);
        }

        _bufferLen = 0;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void Squeeze(System.Span<System.Byte> output)
    {
        // For SHA3-256 we only need 32 bytes; the first rate block is enough.
        // State is in little-endian lanes; we read rate bytes out.
        System.Int32 toWrite = output.Length;
        System.Int32 offset = 0;

        System.Span<System.Byte> laneBytes = stackalloc System.Byte[8];

        while (toWrite > 0)
        {
            System.Int32 lane = 0;

            for (System.Int32 i = 0; i < RateBytes && toWrite > 0; i += 8, lane++)
            {
                System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(laneBytes, _state[lane]);

                System.Int32 chunk = System.Math.Min(8, toWrite);
                laneBytes[..chunk].CopyTo(output.Slice(offset, chunk));
                offset += chunk;
                toWrite -= chunk;
            }

            if (toWrite > 0)
            {
                // Apply another permutation to squeeze more (not reached for 32 bytes).
                KeccakF1600(_state);
            }
        }
    }

    // Rotation offsets r[x,y] as a flat table, and round constants RC
    private static readonly System.Int32[] R =
    [
        0,  36, 3,  41, 18,
        1,  44, 10, 45, 2,
        62, 6,  43, 15, 61,
        28, 55, 25, 21, 56,
        27, 20, 39, 8,  14
    ];

    private static readonly System.UInt64[] RC =
    [
        0x0000000000000001UL, 0x0000000000008082UL,
        0x800000000000808aUL, 0x8000000080008000UL,
        0x000000000000808bUL, 0x0000000080000001UL,
        0x8000000080008081UL, 0x8000000000008009UL,
        0x000000000000008aUL, 0x0000000000000088UL,
        0x0000000080008009UL, 0x000000008000000aUL,
        0x000000008000808bUL, 0x800000000000008bUL,
        0x8000000000008089UL, 0x8000000000008003UL,
        0x8000000000008002UL, 0x8000000000000080UL,
        0x000000000000800aUL, 0x800000008000000aUL,
        0x8000000080008081UL, 0x8000000000008080UL,
        0x0000000080000001UL, 0x8000000080008008UL
    ];

    [System.Diagnostics.DebuggerStepThrough]
    private static void KeccakF1600(System.UInt64[] A)
    {
        // Variables are mapped as a flat array A[5*x + y]
        // Steps: θ, ρ+π, χ, ι
        for (System.Int32 round = 0; round < KeccakRounds; round++)
        {
            // θ step
            System.UInt64 c0 = A[0] ^ A[5] ^ A[10] ^ A[15] ^ A[20];
            System.UInt64 c1 = A[1] ^ A[6] ^ A[11] ^ A[16] ^ A[21];
            System.UInt64 c2 = A[2] ^ A[7] ^ A[12] ^ A[17] ^ A[22];
            System.UInt64 c3 = A[3] ^ A[8] ^ A[13] ^ A[18] ^ A[23];
            System.UInt64 c4 = A[4] ^ A[9] ^ A[14] ^ A[19] ^ A[24];

            System.UInt64 d0 = System.Numerics.BitOperations.RotateLeft(c1, 1) ^ c4;
            System.UInt64 d1 = System.Numerics.BitOperations.RotateLeft(c2, 1) ^ c0;
            System.UInt64 d2 = System.Numerics.BitOperations.RotateLeft(c3, 1) ^ c1;
            System.UInt64 d3 = System.Numerics.BitOperations.RotateLeft(c4, 1) ^ c2;
            System.UInt64 d4 = System.Numerics.BitOperations.RotateLeft(c0, 1) ^ c3;

            A[0] ^= d0; A[5] ^= d0; A[10] ^= d0; A[15] ^= d0; A[20] ^= d0;
            A[1] ^= d1; A[6] ^= d1; A[11] ^= d1; A[16] ^= d1; A[21] ^= d1;
            A[2] ^= d2; A[7] ^= d2; A[12] ^= d2; A[17] ^= d2; A[22] ^= d2;
            A[3] ^= d3; A[8] ^= d3; A[13] ^= d3; A[18] ^= d3; A[23] ^= d3;
            A[4] ^= d4; A[9] ^= d4; A[14] ^= d4; A[19] ^= d4; A[24] ^= d4;

            // ρ and π steps combined
            System.UInt64[] B = new System.UInt64[25];
            for (System.Int32 x = 0; x < 5; x++)
            {
                for (System.Int32 y = 0; y < 5; y++)
                {
                    System.Int32 idx = (5 * x) + y;
                    System.Int32 r = R[idx];
                    System.Int32 X = y;
                    System.Int32 Y = ((2 * x) + (3 * y)) % 5;
                    B[(5 * X) + Y] = System.Numerics.BitOperations.RotateLeft(A[idx], r);
                }
            }

            // χ step
            for (System.Int32 x = 0; x < 5; x++)
            {
                System.Int32 i0 = 5 * x;
                System.UInt64 b0 = B[i0 + 0];
                System.UInt64 b1 = B[i0 + 1];
                System.UInt64 b2 = B[i0 + 2];
                System.UInt64 b3 = B[i0 + 3];
                System.UInt64 b4 = B[i0 + 4];

                A[i0 + 0] = b0 ^ ((~b1) & b2);
                A[i0 + 1] = b1 ^ ((~b2) & b3);
                A[i0 + 2] = b2 ^ ((~b3) & b4);
                A[i0 + 3] = b3 ^ ((~b4) & b0);
                A[i0 + 4] = b4 ^ ((~b0) & b1);
            }

            // ι step
            A[0] ^= RC[round];
        }
    }

    #endregion

    #region Overrides

    /// <inheritdoc/>
    public override System.String ToString() => "SHA3-256";

    #endregion
}
