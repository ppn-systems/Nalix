// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Framework.Cryptography.Hashing;

/// <summary>
/// Implements Keccak-256 (FIPS 202) using the Keccak-f[1600] permutation.
/// </summary>
/// <remarks>
/// <para>
/// This implementation is endian-safe and deterministic across architectures.
/// It supports incremental updates and a one-shot convenience API.
/// </para>
/// <para>
/// Parameters for Keccak-256:
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
[System.Diagnostics.DebuggerDisplay("Disposed={_disposed}, Finalized={_finalized}, Tail={_byteCount}")]
public sealed class Keccak256 : System.IDisposable
{
    #region Constants

    private const System.Byte KeccakRounds = 24;
    private const System.Int32 RateBytes = 136;      // 1088-bit rate
    private const System.Int32 HashSizeBytes = 32;   // 256-bit digest
    private const System.Int32 Lanes = 25;           // 5x5

    #endregion Constants

    #region Fields

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
    private static readonly System.Byte[] Dst;
    private static readonly System.Byte[] Rot;

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

    static Keccak256()
    {
        // Flatten: idx = x + 5*y
        Dst = new System.Byte[25];
        Rot = new System.Byte[25];

        // Rho offsets theo Keccak spec (x,y) -> r[x,y]
        System.Int32[,] R = new System.Int32[5, 5] {
            {  0, 36,  3, 41, 18 },
            {  1, 44, 10, 45,  2 },
            { 62,  6, 43, 15, 61 },
            { 28, 55, 25, 21, 56 },
            { 27, 20, 39,  8, 14 }
        };

        for (System.Int32 x = 0; x < 5; x++)
        {
            for (System.Int32 y = 0; y < 5; y++)
            {
                System.Int32 src = x + (5 * y);                     // flatten src
                System.Int32 X = y;                                 // π: X = y
                System.Int32 Y = ((2 * x) + (3 * y)) % 5;           // π: Y = 2x+3y
                System.Int32 dst = X + (5 * Y);                     // flatten dst

                Dst[src] = (System.Byte)dst;
                Rot[src] = (System.Byte)R[x, y];
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Keccak256"/> class with a zeroed sponge state.
    /// </summary>
    public Keccak256()
    {
        Initialize();
    }

    #endregion Ctors

    #region Static API

    /// <summary>
    /// Computes a SHA3-256 hash for the specified input and returns a new 32-byte array with the digest.
    /// </summary>
    /// <param name="data">The input data to hash.</param>
    /// <returns>A new 32-byte array containing the SHA3-256 digest.</returns>
    /// <exception cref="System.ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Byte[] HashData(System.ReadOnlySpan<System.Byte> data)
    {
        using Keccak256 sha3 = new();
        sha3.Update(data);
        return sha3.Finish();
    }

    /// <summary>
    /// Computes a SHA3-256 hash for the specified input and writes the 32-byte digest into <paramref name="output"/>.
    /// </summary>
    /// <param name="data">The input data to hash.</param>
    /// <param name="output">The destination buffer that will receive the 32-byte digest.</param>
    /// <exception cref="System.ArgumentException">Thrown if <paramref name="output"/> is smaller than 32 bytes.</exception>
    /// <exception cref="System.ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static void HashData(System.ReadOnlySpan<System.Byte> data, System.Span<System.Byte> output)
    {
        if (output.Length < HashSizeBytes)
        {
            throw new System.ArgumentException("Output buffer must be at least 32 bytes.", nameof(output));
        }

        using Keccak256 sha3 = new();
        sha3.Update(data);
        sha3.Finish(output);
    }

    #endregion Static API

    #region Public API

    /// <summary>
    /// Resets the internal state and clears any buffered data and prior result.
    /// </summary>
    /// <remarks>
    /// After calling <see cref="Initialize"/>, the instance can be reused for a new hashing operation.
    /// </remarks>
    [System.Diagnostics.DebuggerNonUserCode]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
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
    /// Incrementally absorbs input data into the sponge state.
    /// </summary>
    /// <param name="data">The input data to absorb.</param>
    /// <remarks>
    /// Call <see cref="Finish()"/> or <see cref="Finish(System.Span{System.Byte})"/> to complete hashing and retrieve the digest.
    /// </remarks>
    /// <exception cref="System.ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    /// <exception cref="System.InvalidOperationException">Thrown if called after the hash has already been finalized.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void Update(System.ReadOnlySpan<System.Byte> data)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(Keccak256));
        if (_finalized)
        {
            throw new System.InvalidOperationException("Cannot update after finalization.");
        }

        System.ReadOnlySpan<System.Byte> input = data;

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
                this.AbsorbBlock(_buffer);
                _bufferLen = 0;
            }
        }

        // Process full blocks direct from input
        while (input.Length >= RateBytes)
        {
            this.AbsorbBlock(input[..RateBytes]);
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Byte[] Finish()
    {
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(Keccak256));
        if (_finalized && _finalHash != null)
        {
            return (System.Byte[])_finalHash.Clone();
        }

        this.Pad();

        System.Byte[] result = System.GC.AllocateUninitializedArray<System.Byte>(HashSizeBytes);
        this.Squeeze(result);

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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void Finish(System.Span<System.Byte> output)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(Keccak256));
        if (output.Length < HashSizeBytes)
        {
            throw new System.ArgumentException("Output buffer must be at least 32 bytes.", nameof(output));
        }

        if (_finalized && _finalHash != null)
        {
            System.MemoryExtensions.AsSpan(_finalHash)
                                   .CopyTo(output);
            return;
        }

        this.Pad();
        this.Squeeze(output[..HashSizeBytes]);

        _finalHash = output[..HashSizeBytes].ToArray();
        _finalized = true;
    }

    /// <summary>
    /// Computes a SHA3-256 hash for the specified input and returns a new 32-byte array with the digest.
    /// </summary>
    /// <param name="data">The input data to hash.</param>
    /// <returns>A new 32-byte array containing the SHA3-256 digest.</returns>
    /// <remarks>
    /// Equivalent to calling <see cref="Update(System.ReadOnlySpan{System.Byte})"/> followed by <see cref="Finish()"/>.
    /// </remarks>
    /// <exception cref="System.ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Byte[] ComputeHash(System.ReadOnlySpan<System.Byte> data)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(Keccak256));

        using Keccak256 tmp = new();
        tmp.Update(data);
        return tmp.Finish();
    }

    /// <summary>
    /// Computes a SHA3-256 hash for the specified input and returns a new 32-byte array with the digest.
    /// </summary>
    /// <param name="data">The input data to hash.</param>
    /// <param name="output">The destination buffer that will receive the 32-byte digest.</param>
    /// <returns>A new 32-byte array containing the SHA3-256 digest.</returns>
    /// <remarks>
    /// Equivalent to calling <see cref="Update(System.ReadOnlySpan{System.Byte})"/> followed by <see cref="Finish()"/>.
    /// </remarks>
    /// <exception cref="System.ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void ComputeHash(System.ReadOnlySpan<System.Byte> data, System.Span<System.Byte> output)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(Keccak256));

        using Keccak256 tmp = new();
        tmp.Update(data);
        tmp.Finish(output);
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
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private unsafe void AbsorbBlock(System.ReadOnlySpan<System.Byte> block)
    {
        System.Diagnostics.Debug.Assert(block.Length == RateBytes);

        // Fast little-endian paths (vectorized), else fall back to deterministic big-endian scalar.
        if (System.BitConverter.IsLittleEndian)
        {
            fixed (System.Byte* pBlock = block)
            {
                fixed (System.UInt64* pState = _state)
                {
                    // --- AVX-512 (2 × 64B) + tail ---
                    if (System.Runtime.Intrinsics.X86.Avx512F.IsSupported && System.Runtime.Intrinsics.X86.Avx512DQ.IsSupported)
                    {
                        var b0 = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<
                                    System.Runtime.Intrinsics.Vector512<System.UInt64>>(pBlock + 0);
                        var b1 = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<
                                    System.Runtime.Intrinsics.Vector512<System.UInt64>>(pBlock + 64);

                        var s0 = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<
                                    System.Runtime.Intrinsics.Vector512<System.UInt64>>((System.Byte*)pState + 0);
                        var s1 = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<
                                    System.Runtime.Intrinsics.Vector512<System.UInt64>>((System.Byte*)pState + 64);

                        var x0 = System.Runtime.Intrinsics.X86.Avx512F.Xor(b0, s0);
                        var x1 = System.Runtime.Intrinsics.X86.Avx512F.Xor(b1, s1);

                        System.Runtime.CompilerServices.Unsafe.WriteUnaligned((System.Byte*)pState + 0, x0);
                        System.Runtime.CompilerServices.Unsafe.WriteUnaligned((System.Byte*)pState + 64, x1);

                        pState[16] ^= System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(128, 8));
                        KeccakF1600(_state);
                        return;
                    }

                    // --- AVX2 (4 × 32B) + tail ---
                    if (System.Runtime.Intrinsics.X86.Avx2.IsSupported)
                    {
                        for (System.Int32 off = 0; off < 128; off += 32)
                        {
                            var vb = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<
                                        System.Runtime.Intrinsics.Vector256<System.UInt64>>(pBlock + off);
                            var vs = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<
                                        System.Runtime.Intrinsics.Vector256<System.UInt64>>((System.Byte*)pState + off);

                            var vx = System.Runtime.Intrinsics.X86.Avx2.Xor(vb, vs);
                            System.Runtime.CompilerServices.Unsafe.WriteUnaligned((System.Byte*)pState + off, vx);
                        }

                        pState[16] ^= System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(128, 8));
                        KeccakF1600(_state);
                        return;
                    }

                    // --- ARM AdvSimd (8 × 16B) + tail ---
                    if (System.Runtime.Intrinsics.Arm.AdvSimd.IsSupported)
                    {
                        // XOR theo từng Vector128<ulong> (16B) – giống SSE2 path
                        for (System.Int32 off = 0; off < 128; off += 16)
                        {
                            var vb = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<
                                        System.Runtime.Intrinsics.Vector128<System.UInt64>>(pBlock + off);
                            var vs = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<
                                        System.Runtime.Intrinsics.Vector128<System.UInt64>>((System.Byte*)pState + off);
                            var vx = System.Runtime.Intrinsics.Arm.AdvSimd.Xor(vb, vs);
                            System.Runtime.CompilerServices.Unsafe.WriteUnaligned((System.Byte*)pState + off, vx);
                        }
                        pState[16] ^= System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(128, 8));
                        KeccakF1600(_state);
                        return;
                    }

                    // --- SSE2 (8 × 16B) + tail ---
                    if (System.Runtime.Intrinsics.X86.Sse2.IsSupported)
                    {
                        for (System.Int32 off = 0; off < 128; off += 16)
                        {
                            // Process 16B as Vector128<ulong> (2 lanes)
                            var vb = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<
                                        System.Runtime.Intrinsics.Vector128<System.UInt64>>(pBlock + off);
                            var vs = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<
                                        System.Runtime.Intrinsics.Vector128<System.UInt64>>((System.Byte*)pState + off);

                            var vx = System.Runtime.Intrinsics.X86.Sse2.Xor(vb, vs);
                            System.Runtime.CompilerServices.Unsafe.WriteUnaligned((System.Byte*)pState + off, vx);
                        }

                        pState[16] ^= System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(128, 8));
                        KeccakF1600(_state);
                        return;
                    }

                    // --- Scalar little-endian: cast to ulong[] (bounds-check free with ref-add) ---
                    System.ReadOnlySpan<System.UInt64> lanes = System.Runtime.InteropServices.MemoryMarshal.Cast<System.Byte, System.UInt64>(block);
                    ref System.UInt64 state0 = ref _state[0];

                    #region Unrolled XOR

                    System.Runtime.CompilerServices.Unsafe.Add(ref state0, 0) ^= lanes[0];
                    System.Runtime.CompilerServices.Unsafe.Add(ref state0, 1) ^= lanes[1];
                    System.Runtime.CompilerServices.Unsafe.Add(ref state0, 2) ^= lanes[2];
                    System.Runtime.CompilerServices.Unsafe.Add(ref state0, 3) ^= lanes[3];
                    System.Runtime.CompilerServices.Unsafe.Add(ref state0, 4) ^= lanes[4];
                    System.Runtime.CompilerServices.Unsafe.Add(ref state0, 5) ^= lanes[5];
                    System.Runtime.CompilerServices.Unsafe.Add(ref state0, 6) ^= lanes[6];
                    System.Runtime.CompilerServices.Unsafe.Add(ref state0, 7) ^= lanes[7];
                    System.Runtime.CompilerServices.Unsafe.Add(ref state0, 8) ^= lanes[8];
                    System.Runtime.CompilerServices.Unsafe.Add(ref state0, 9) ^= lanes[9];
                    System.Runtime.CompilerServices.Unsafe.Add(ref state0, 10) ^= lanes[10];
                    System.Runtime.CompilerServices.Unsafe.Add(ref state0, 11) ^= lanes[11];
                    System.Runtime.CompilerServices.Unsafe.Add(ref state0, 12) ^= lanes[12];
                    System.Runtime.CompilerServices.Unsafe.Add(ref state0, 13) ^= lanes[13];
                    System.Runtime.CompilerServices.Unsafe.Add(ref state0, 14) ^= lanes[14];
                    System.Runtime.CompilerServices.Unsafe.Add(ref state0, 15) ^= lanes[15];
                    System.Runtime.CompilerServices.Unsafe.Add(ref state0, 16) ^= lanes[16];

                    #endregion Unrolled XOR

                    KeccakF1600(_state);
                    return;
                }
            }
        }

        // --- Big-endian: consume as little-endian per lane for determinism ---
        ref System.UInt64 dst = ref _state[0];

        #region Unrolled Read+XOR

        System.UInt64 v0 = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(block[..8]);
        System.Runtime.CompilerServices.Unsafe.Add(ref dst, 0) ^= v0;

        System.UInt64 v1 = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(8, 8));
        System.Runtime.CompilerServices.Unsafe.Add(ref dst, 1) ^= v1;

        System.UInt64 v2 = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(16, 8));
        System.Runtime.CompilerServices.Unsafe.Add(ref dst, 2) ^= v2;

        System.UInt64 v3 = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(24, 8));
        System.Runtime.CompilerServices.Unsafe.Add(ref dst, 3) ^= v3;

        System.UInt64 v4 = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(32, 8));
        System.Runtime.CompilerServices.Unsafe.Add(ref dst, 4) ^= v4;

        System.UInt64 v5 = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(40, 8));
        System.Runtime.CompilerServices.Unsafe.Add(ref dst, 5) ^= v5;

        System.UInt64 v6 = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(48, 8));
        System.Runtime.CompilerServices.Unsafe.Add(ref dst, 6) ^= v6;

        System.UInt64 v7 = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(56, 8));
        System.Runtime.CompilerServices.Unsafe.Add(ref dst, 7) ^= v7;

        System.UInt64 v8 = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(64, 8));
        System.Runtime.CompilerServices.Unsafe.Add(ref dst, 8) ^= v8;

        System.UInt64 v9 = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(72, 8));
        System.Runtime.CompilerServices.Unsafe.Add(ref dst, 9) ^= v9;

        System.UInt64 v10 = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(80, 8));
        System.Runtime.CompilerServices.Unsafe.Add(ref dst, 10) ^= v10;

        System.UInt64 v11 = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(88, 8));
        System.Runtime.CompilerServices.Unsafe.Add(ref dst, 11) ^= v11;

        System.UInt64 v12 = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(96, 8));
        System.Runtime.CompilerServices.Unsafe.Add(ref dst, 12) ^= v12;

        System.UInt64 v13 = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(104, 8));
        System.Runtime.CompilerServices.Unsafe.Add(ref dst, 13) ^= v13;

        System.UInt64 v14 = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(112, 8));
        System.Runtime.CompilerServices.Unsafe.Add(ref dst, 14) ^= v14;

        System.UInt64 v15 = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(120, 8));
        System.Runtime.CompilerServices.Unsafe.Add(ref dst, 15) ^= v15;

        System.UInt64 v16 = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(128, 8));
        System.Runtime.CompilerServices.Unsafe.Add(ref dst, 16) ^= v16;

        #endregion Unrolled Read+XOR

        KeccakF1600(_state);
    }


    /// <summary>
    /// Applies SHA-3 domain padding (0x06 ... 0x80) and absorbs the final block(s).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private void Pad()
    {
        System.Span<System.Byte> pad = _buffer;
        System.Int32 n = _bufferLen;

        if (n == RateBytes)
        {
            AbsorbBlock(pad);
            n = 0;
        }

        // Zero the tail explicitly to be crystal-clear
        if (n < RateBytes)
        {
            pad[n..].Clear();
        }

        pad[n] = 0x06;

        if (n == RateBytes - 1)
        {
            // No room for 0x80 here, absorb and make a full zero block with 0x80 at end
            AbsorbBlock(pad);
            pad.Clear();
            pad[RateBytes - 1] = 0x80;
            AbsorbBlock(pad);
        }
        else
        {
            pad[RateBytes - 1] |= 0x80;
            AbsorbBlock(pad);
        }

        _bufferLen = 0;
    }

    /// <summary>
    /// Writes the first 32 bytes of the state (little-endian lanes A0..A3) into <paramref name="output"/>.
    /// </summary>
    /// <param name="output">Destination span of at least 32 bytes.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private unsafe void Squeeze(System.Span<System.Byte> output)
    {
        // Always write as little-endian lanes explicitly (portable)
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(output[..8], _state[0]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(output.Slice(8, 8), _state[1]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(output.Slice(16, 8), _state[2]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(output.Slice(24, 8), _state[3]);
    }

    /// <summary>
    /// Applies 24 rounds of the Keccak-f[1600] permutation in-place.
    /// </summary>
    /// <param name="A">The 5×5×64-bit sponge state arranged as a 25-element array.</param>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void KeccakF1600(System.UInt64[] A)
    {
        // Single scratch buffer on the stack.
        System.Span<System.UInt64> B = stackalloc System.UInt64[Lanes];

        for (System.Int32 round = 0; round < KeccakRounds; round++)
        {
            // θ
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

            // Apply D to columns – flattened to avoid loop overhead.
            A[0] ^= d0; A[5] ^= d0; A[10] ^= d0; A[15] ^= d0; A[20] ^= d0;
            A[1] ^= d1; A[6] ^= d1; A[11] ^= d1; A[16] ^= d1; A[21] ^= d1;
            A[2] ^= d2; A[7] ^= d2; A[12] ^= d2; A[17] ^= d2; A[22] ^= d2;
            A[3] ^= d3; A[8] ^= d3; A[13] ^= d3; A[18] ^= d3; A[23] ^= d3;
            A[4] ^= d4; A[9] ^= d4; A[14] ^= d4; A[19] ^= d4; A[24] ^= d4;

            // ρ + π (use precomputed Dst/Rot)
            #region Unrolled

            B[Dst[0]] = System.Numerics.BitOperations.RotateLeft(A[0], Rot[0]);
            B[Dst[1]] = System.Numerics.BitOperations.RotateLeft(A[1], Rot[1]);
            B[Dst[2]] = System.Numerics.BitOperations.RotateLeft(A[2], Rot[2]);
            B[Dst[3]] = System.Numerics.BitOperations.RotateLeft(A[3], Rot[3]);
            B[Dst[4]] = System.Numerics.BitOperations.RotateLeft(A[4], Rot[4]);

            B[Dst[5]] = System.Numerics.BitOperations.RotateLeft(A[5], Rot[5]);
            B[Dst[6]] = System.Numerics.BitOperations.RotateLeft(A[6], Rot[6]);
            B[Dst[7]] = System.Numerics.BitOperations.RotateLeft(A[7], Rot[7]);
            B[Dst[8]] = System.Numerics.BitOperations.RotateLeft(A[8], Rot[8]);
            B[Dst[9]] = System.Numerics.BitOperations.RotateLeft(A[9], Rot[9]);

            B[Dst[10]] = System.Numerics.BitOperations.RotateLeft(A[10], Rot[10]);
            B[Dst[11]] = System.Numerics.BitOperations.RotateLeft(A[11], Rot[11]);
            B[Dst[12]] = System.Numerics.BitOperations.RotateLeft(A[12], Rot[12]);
            B[Dst[13]] = System.Numerics.BitOperations.RotateLeft(A[13], Rot[13]);
            B[Dst[14]] = System.Numerics.BitOperations.RotateLeft(A[14], Rot[14]);

            B[Dst[15]] = System.Numerics.BitOperations.RotateLeft(A[15], Rot[15]);
            B[Dst[16]] = System.Numerics.BitOperations.RotateLeft(A[16], Rot[16]);
            B[Dst[17]] = System.Numerics.BitOperations.RotateLeft(A[17], Rot[17]);
            B[Dst[18]] = System.Numerics.BitOperations.RotateLeft(A[18], Rot[18]);
            B[Dst[19]] = System.Numerics.BitOperations.RotateLeft(A[19], Rot[19]);

            B[Dst[20]] = System.Numerics.BitOperations.RotateLeft(A[20], Rot[20]);
            B[Dst[21]] = System.Numerics.BitOperations.RotateLeft(A[21], Rot[21]);
            B[Dst[22]] = System.Numerics.BitOperations.RotateLeft(A[22], Rot[22]);
            B[Dst[23]] = System.Numerics.BitOperations.RotateLeft(A[23], Rot[23]);
            B[Dst[24]] = System.Numerics.BitOperations.RotateLeft(A[24], Rot[24]);

            #endregion Unrolled

            // χ
            #region Unrolled

            const System.Int32 i0 = 0;
            System.UInt64 b0 = B[i0 + 0], b1 = B[i0 + 1], b2 = B[i0 + 2], b3 = B[i0 + 3], b4 = B[i0 + 4];
            A[i0 + 0] = b0 ^ ((~b1) & b2);
            A[i0 + 1] = b1 ^ ((~b2) & b3);
            A[i0 + 2] = b2 ^ ((~b3) & b4);
            A[i0 + 3] = b3 ^ ((~b4) & b0);
            A[i0 + 4] = b4 ^ ((~b0) & b1);

            const System.Int32 i1 = 5;
            b0 = B[i1 + 0]; b1 = B[i1 + 1]; b2 = B[i1 + 2]; b3 = B[i1 + 3]; b4 = B[i1 + 4];
            A[i1 + 0] = b0 ^ ((~b1) & b2);
            A[i1 + 1] = b1 ^ ((~b2) & b3);
            A[i1 + 2] = b2 ^ ((~b3) & b4);
            A[i1 + 3] = b3 ^ ((~b4) & b0);
            A[i1 + 4] = b4 ^ ((~b0) & b1);

            const System.Int32 i2 = 10;
            b0 = B[i2 + 0]; b1 = B[i2 + 1]; b2 = B[i2 + 2]; b3 = B[i2 + 3]; b4 = B[i2 + 4];
            A[i2 + 0] = b0 ^ ((~b1) & b2);
            A[i2 + 1] = b1 ^ ((~b2) & b3);
            A[i2 + 2] = b2 ^ ((~b3) & b4);
            A[i2 + 3] = b3 ^ ((~b4) & b0);
            A[i2 + 4] = b4 ^ ((~b0) & b1);

            const System.Int32 i3 = 15;
            b0 = B[i3 + 0]; b1 = B[i3 + 1]; b2 = B[i3 + 2]; b3 = B[i3 + 3]; b4 = B[i3 + 4];
            A[i3 + 0] = b0 ^ ((~b1) & b2);
            A[i3 + 1] = b1 ^ ((~b2) & b3);
            A[i3 + 2] = b2 ^ ((~b3) & b4);
            A[i3 + 3] = b3 ^ ((~b4) & b0);
            A[i3 + 4] = b4 ^ ((~b0) & b1);

            const System.Int32 i4 = 20;
            b0 = B[i4 + 0]; b1 = B[i4 + 1]; b2 = B[i4 + 2]; b3 = B[i4 + 3]; b4 = B[i4 + 4];
            A[i4 + 0] = b0 ^ ((~b1) & b2);
            A[i4 + 1] = b1 ^ ((~b2) & b3);
            A[i4 + 2] = b2 ^ ((~b3) & b4);
            A[i4 + 3] = b3 ^ ((~b4) & b0);
            A[i4 + 4] = b4 ^ ((~b0) & b1);

            #endregion Unrolled

            // ι
            A[0] ^= RC[round];
        }
    }

    #endregion Keccak Core (private)

    #region Class

    private static class Padding
    {
        internal static readonly System.Byte[] Tail = CreateTail();

        private static System.Byte[] CreateTail()
        {
            System.Byte[] b = new System.Byte[RateBytes];
            b[RateBytes - 1] = 0x80;
            return b;
        }
    }

    #endregion Class

    #region Overrides

    /// <summary>
    /// Returns the algorithm display name.
    /// </summary>
    /// <returns>The string <c>Keccak-256</c>.</returns>
    public override System.String ToString() => "Keccak-256";

    #endregion Overrides
}
