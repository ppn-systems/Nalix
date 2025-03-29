// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Abstractions;

namespace Nalix.Framework.Cryptography.Hashing;

/// <summary>
/// Implements SHA3-256 (FIPS 202) using the Keccak-f[1600] permutation.
/// </summary>
/// <remarks>
/// <para>
/// This implementation is endian-safe and deterministic across architectures.
/// It supports incremental updates and a one-shot convenience API.
/// </para>
/// <para>
/// Parameters for SHA3-256:
/// <list type="bullet">
/// <item><description>State width: 1600 bits (25 lanes × 64 bits)</description></item>
/// <item><description>Rate: 1088 bits (136 bytes)</description></item>
/// <item><description>Capacity: 512 bits</description></item>
/// <item><description>Padding: multi-rate domain 0x06 with final 0x80 bit</description></item>
/// </list>
/// </para>
/// <para>
/// Performance notes:
/// - On little-endian systems the absorber uses vectorized XOR paths (AVX2/AVX-512 when available).
/// - The sponge state is kept in a managed <see cref="System.UInt64"/> array to avoid unsafe aliasing issues.
/// </para>
/// </remarks>
/// <threadsafety>
/// <para>
/// Instances are <b>not</b> thread-safe. Do not share a single instance across threads
/// without external synchronization. Use one instance per hashing operation.
/// </para>
/// </threadsafety>
/// <seealso href="https://nvlpubs.nist.gov/nistpubs/FIPS/NIST.FIPS.202.pdf">FIPS 202: SHA-3 Standard</seealso>
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Runtime.InteropServices.ComVisible(true)]
[System.Diagnostics.DebuggerDisplay("Disposed={_disposed}, Finalized={_finalized}, Bytes={_byteCount}")]
public sealed class SHA3256 : IShaDigest, System.IDisposable
{
    #region Constants

    private const System.Byte KeccakRounds = 24;
    private const System.Int32 RateBytes = 136;      // 1088-bit rate
    private const System.Int32 HashSizeBytes = 32;   // 256-bit digest
    private const System.Int32 Lanes = 25;           // 5x5

    #endregion Constants

    #region Fields

    // 25 lanes of 64-bit
    private readonly System.UInt64[] _state = new System.UInt64[Lanes];

    // Absorb buffer (rate area)
    private readonly System.Byte[] _buffer = new System.Byte[RateBytes];
    private System.Int32 _bufferLen;

    private System.Boolean _finalized;
    private System.Boolean _disposed;
    private System.UInt64 _byteCount;
    private System.Byte[]? _finalHash;

    #endregion Fields

    #region Ctors

    static SHA3256()
    {
#if DEBUG
        // Verify Dst against formula: idx = 5*x + y; X = y; Y = (2x + 3y) % 5; dst = 5*X + Y
        var calc = new System.Byte[25];
        for (System.Int32 idx = 0; idx < 25; idx++)
        {
            System.Int32 x = idx / 5, y = idx % 5;
            System.Int32 X = y;
            System.Int32 Y = ((2 * x) + (3 * y)) % 5;
            calc[idx] = (System.Byte)((5 * X) + Y);
        }
        for (System.Int32 i = 0; i < 25; i++)
        {
            System.Diagnostics.Debug.Assert(Dst[i] == calc[i], "Dst mapping mismatch at " + i);
        }
        // Rot must match the flattened R for Src[i] == i
#endif
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SHA3256"/> class with a zeroed sponge state.
    /// </summary>
    public SHA3256() => Initialize();

    #endregion Ctors

    #region Public API

    /// <summary>
    /// Resets the internal state and clears any buffered data and prior result.
    /// </summary>
    /// <remarks>
    /// After calling <see cref="Initialize"/>, the instance can be reused for a new hashing operation.
    /// </remarks>
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
            _finalHash = null!;
        }
    }

    /// <summary>
    /// Computes a SHA3-256 hash for the specified input and returns a new 32-byte array with the digest.
    /// </summary>
    /// <param name="data">The input data to hash.</param>
    /// <returns>A new 32-byte array containing the SHA3-256 digest.</returns>
    /// <exception cref="System.ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    [System.Diagnostics.Contracts.Pure]
    public static System.Byte[] HashData(System.ReadOnlySpan<System.Byte> data)
    {
        using var sha3 = new SHA3256();
        sha3.Update(data);
        return sha3.FinalizeHash();
    }

    /// <summary>
    /// Computes a SHA3-256 hash for the specified input and writes the 32-byte digest into <paramref name="output"/>.
    /// </summary>
    /// <param name="data">The input data to hash.</param>
    /// <param name="output">The destination buffer that will receive the 32-byte digest.</param>
    /// <exception cref="System.ArgumentException">Thrown if <paramref name="output"/> is smaller than 32 bytes.</exception>
    /// <exception cref="System.ObjectDisposedException">Thrown if the instance has been disposed.</exception>
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
    /// Incrementally absorbs input data into the sponge state.
    /// </summary>
    /// <param name="data">The input data to absorb.</param>
    /// <remarks>
    /// Call <see cref="FinalizeHash()"/> or <see cref="FinalizeHash(System.Span{System.Byte})"/> to complete hashing and retrieve the digest.
    /// </remarks>
    /// <exception cref="System.ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    /// <exception cref="System.InvalidOperationException">Thrown if called after the hash has already been finalized.</exception>
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

        // Fill tail of the partial block if any
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

        // Process full blocks direct from input
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
    /// Finalizes the hash computation and returns a new 32-byte array with the digest.
    /// </summary>
    /// <returns>A new 32-byte array containing the SHA3-256 digest.</returns>
    /// <remarks>
    /// Subsequent calls return a clone of the cached result without mutating state.
    /// </remarks>
    /// <exception cref="System.ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    [System.Diagnostics.Contracts.Pure]
    public System.Byte[] FinalizeHash()
    {
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(SHA3256));
        if (_finalized && _finalHash != null)
        {
            return (System.Byte[])_finalHash.Clone();
        }

        ApplyPaddingAndAbsorb();

        var result = System.GC.AllocateUninitializedArray<System.Byte>(HashSizeBytes);
        Squeeze(result);
        _finalHash = result;
        _finalized = true;
        return (System.Byte[])_finalHash.Clone();
    }

    /// <summary>
    /// Finalizes the hash computation and writes the 32-byte digest into <paramref name="output"/>.
    /// </summary>
    /// <param name="output">The destination buffer that will receive the 32-byte digest.</param>
    /// <exception cref="System.ArgumentException">Thrown if <paramref name="output"/> is smaller than 32 bytes.</exception>
    /// <exception cref="System.ObjectDisposedException">Thrown if the instance has been disposed.</exception>
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

        Squeeze(output[..HashSizeBytes]);
        _finalHash = output[..HashSizeBytes].ToArray();
        _finalized = true;
    }

    /// <summary>
    /// Computes a SHA3-256 hash for the specified input and returns a new 32-byte array with the digest.
    /// </summary>
    /// <param name="data">The input data to hash.</param>
    /// <returns>A new 32-byte array containing the SHA3-256 digest.</returns>
    /// <remarks>
    /// Equivalent to calling <see cref="Update(System.ReadOnlySpan{System.Byte})"/> followed by <see cref="FinalizeHash()"/>.
    /// </remarks>
    /// <exception cref="System.ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    public System.Byte[] ComputeHash(System.ReadOnlySpan<System.Byte> data)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(SHA3256));
        Update(data);
        var result = System.GC.AllocateUninitializedArray<System.Byte>(HashSizeBytes);
        FinalizeHash(result);
        return result;
    }

    #endregion Public API

    #region IDisposable

    /// <summary>
    /// Releases resources used by the current instance and clears sensitive buffers.
    /// </summary>
    /// <remarks>
    /// After disposal, the instance becomes unusable and further calls may throw <see cref="System.ObjectDisposedException"/>.
    /// </remarks>
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

    #endregion IDisposable

    #region Keccak Core (private)

    /// <summary>
    /// Absorbs one full rate block (136 bytes) into the state and applies Keccak-f[1600].
    /// </summary>
    /// <param name="block">The 136-byte block.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private unsafe void AbsorbBlock(System.ReadOnlySpan<System.Byte> block)
    {
        System.Diagnostics.Debug.Assert(block.Length == RateBytes);

        // XOR rate portion into state (17 lanes)
        if (System.BitConverter.IsLittleEndian &&
            System.Runtime.Intrinsics.X86.Avx512F.IsSupported &&
            System.Runtime.Intrinsics.X86.Avx512DQ.IsSupported)
        {
            fixed (System.Byte* pBlock = block)
            {
                fixed (System.UInt64* pState = _state)
                {
                    var b0 = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<
                        System.Runtime.Intrinsics.Vector512<System.UInt64>>(pBlock + 0);

                    var s0 = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<
                        System.Runtime.Intrinsics.Vector512<System.UInt64>>((System.Byte*)pState + 0);

                    var b1 = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<
                        System.Runtime.Intrinsics.Vector512<System.UInt64>>(pBlock + 64);

                    var s1 = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<
                        System.Runtime.Intrinsics.Vector512<System.UInt64>>((System.Byte*)pState + 64);

                    var x0 = System.Runtime.Intrinsics.X86.Avx512F.Xor(b0, s0);
                    var x1 = System.Runtime.Intrinsics.X86.Avx512F.Xor(b1, s1);

                    System.Runtime.CompilerServices.Unsafe.WriteUnaligned((System.Byte*)pState + 0, x0);
                    System.Runtime.CompilerServices.Unsafe.WriteUnaligned((System.Byte*)pState + 64, x1);

                    System.UInt64 tail = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(128, 8));
                    pState[16] ^= tail;
                }
            }

            KeccakF1600(_state);
            return;
        }
        else if (System.BitConverter.IsLittleEndian &&
                 System.Runtime.Intrinsics.X86.Avx2.IsSupported)
        {
            fixed (System.Byte* pBlock = block)
            {
                fixed (System.UInt64* pState = _state)
                {
                    // 128 bytes: 4 × 32 bytes (Vector256<ulong>)
                    for (System.Int32 off = 0; off < 128; off += 32)
                    {
                        var vBlock = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<
                            System.Runtime.Intrinsics.Vector256<System.UInt64>>(pBlock + off);

                        var vState = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<
                            System.Runtime.Intrinsics.Vector256<System.UInt64>>((System.Byte*)pState + off);

                        var vXor = System.Runtime.Intrinsics.X86.Avx2.Xor(vBlock, vState);
                        System.Runtime.CompilerServices.Unsafe.WriteUnaligned((System.Byte*)pState + off, vXor);
                    }

                    // remaining 8 bytes (lane 17)
                    System.UInt64 tail = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(128, 8));
                    pState[16] ^= tail;
                }
            }
        }
        else if (System.BitConverter.IsLittleEndian)
        {
            // Fast path scalar LE: cast to ulong
            System.ReadOnlySpan<System.UInt64> lanes = System.Runtime.InteropServices.MemoryMarshal.Cast<System.Byte, System.UInt64>(block);
            ref System.UInt64 s0 = ref _state[0];
            for (System.Int32 i = 0; i < 17; i++)
            {
                System.Runtime.CompilerServices.Unsafe.Add(ref s0, i) ^= lanes[i];
            }
        }
        else
        {
            // Big-endian: read each lane as LE to keep determinism
            ref System.UInt64 s0 = ref _state[0];
            for (System.Int32 i = 0, off = 0; i < 17; i++, off += 8)
            {
                System.UInt64 v = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(off, 8));
                System.Runtime.CompilerServices.Unsafe.Add(ref s0, i) ^= v;
            }
        }

        KeccakF1600(_state);
    }

    /// <summary>
    /// Applies SHA-3 domain padding (0x06 ... 0x80) and absorbs the final block(s).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void ApplyPaddingAndAbsorb()
    {
        System.Span<System.Byte> pad = System.MemoryExtensions.AsSpan(_buffer);
        System.Int32 n = _bufferLen;

        if (n == RateBytes)
        {
            AbsorbBlock(pad);
            n = 0;
        }

        pad[n++] = 0x06;
        if (n < RateBytes)
        {
            pad[n..RateBytes].Clear();
            pad[RateBytes - 1] |= 0x80;
            AbsorbBlock(pad);
        }
        else
        {
            AbsorbBlock(pad);
            pad.Clear();
            pad[RateBytes - 1] = 0x80;
            AbsorbBlock(pad);
        }

        _bufferLen = 0;
    }

    /// <summary>
    /// Writes the first 32 bytes of the state (little-endian lanes A0..A3) into <paramref name="output"/>.
    /// </summary>
    /// <param name="output">Destination span of at least 32 bytes.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private unsafe void Squeeze(System.Span<System.Byte> output)
    {
        System.Diagnostics.Debug.Assert(output.Length >= 32);

        if (System.BitConverter.IsLittleEndian)
        {
            fixed (System.Byte* pOut = output)
            {
                fixed (System.UInt64* pState = _state)
                {
                    System.Buffer.MemoryCopy(pState, pOut, 32, 32);
                }
            }
        }
        else
        {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(output[..8], _state[0]);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(output.Slice(8, 8), _state[1]);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(output.Slice(16, 8), _state[2]);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(output.Slice(24, 8), _state[3]);
        }
    }

    // Round constants
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

    // Map for Rho+Pi: for i in 0..24: B[Dst[i]] = ROT(A[Src[i]], Rot[i])
    private static System.ReadOnlySpan<System.Byte> Dst =>
    [
          0,  6, 12, 18, 24, 3,  9, 10,
         16, 22,  1,  7, 13, 19, 20, 4,
          5, 11, 17, 23,  2,  8, 14, 15, 21
    ];

    private static System.ReadOnlySpan<System.Byte> Rot =>
    [
         0, 36,  3, 41, 18, 1, 44, 10, 45,  2,
        62,  6, 43, 15, 61,28, 55, 25, 21, 56,
        27, 20, 39,  8, 14
    ];

    /// <summary>
    /// Applies 24 rounds of the Keccak-f[1600] permutation in-place.
    /// </summary>
    /// <param name="A">The 5×5×64-bit sponge state arranged as a 25-element array.</param>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void KeccakF1600(System.UInt64[] A)
    {
        // Reuse a single scratch buffer B (avoids stackalloc per round)
        System.Span<System.UInt64> B = stackalloc System.UInt64[Lanes];

        for (System.Int32 round = 0; round < KeccakRounds; round++)
        {
            // θ (theta)
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

            // apply D to columns
            A[0] ^= d0; A[5] ^= d0; A[10] ^= d0; A[15] ^= d0; A[20] ^= d0;
            A[1] ^= d1; A[6] ^= d1; A[11] ^= d1; A[16] ^= d1; A[21] ^= d1;
            A[2] ^= d2; A[7] ^= d2; A[12] ^= d2; A[17] ^= d2; A[22] ^= d2;
            A[3] ^= d3; A[8] ^= d3; A[13] ^= d3; A[18] ^= d3; A[23] ^= d3;
            A[4] ^= d4; A[9] ^= d4; A[14] ^= d4; A[19] ^= d4; A[24] ^= d4;

            // ρ + π (rho + pi) into B
            for (System.Int32 i = 0; i < 25; i++)
            {
                System.UInt64 v = A[i];
                System.Int32 r = Rot[i];
                B[Dst[i]] = System.Numerics.BitOperations.RotateLeft(v, r);
            }

            // χ (chi) back into A row-wise
            for (System.Int32 x = 0; x < 5; x++)
            {
                System.Int32 i = 5 * x;
                System.UInt64 b0 = B[i + 0], b1 = B[i + 1], b2 = B[i + 2], b3 = B[i + 3], b4 = B[i + 4];

                System.UInt64 nb1 = ~b1, nb2 = ~b2, nb3 = ~b3, nb4 = ~b4, nb0 = ~b0;

                A[i + 0] = b0 ^ (nb1 & b2);
                A[i + 1] = b1 ^ (nb2 & b3);
                A[i + 2] = b2 ^ (nb3 & b4);
                A[i + 3] = b3 ^ (nb4 & b0);
                A[i + 4] = b4 ^ (nb0 & b1);
            }

            // ι (iota)
            A[0] ^= RC[round];
        }
    }

    #endregion Keccak Core (private)

    #region Overrides

    /// <summary>
    /// Returns the algorithm display name.
    /// </summary>
    /// <returns>The string <c>SHA3-256</c>.</returns>
    public override System.String ToString() => "SHA3-256";

    #endregion Overrides
}
