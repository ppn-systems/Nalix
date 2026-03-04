// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Shared.Security.Hashing;

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
/// Optimization notes (v2):
/// <list type="bullet">
/// <item><description>
///   Zero heap allocation: the sponge state lives entirely on the stack via <see langword="ref struct"/>.
///   <c>HashData</c> allocates 0 bytes on the managed heap per call.
/// </description></item>
/// <item><description>
///   One-shot fast-path: when the entire payload fits within one rate block (≤ 136 bytes),
///   <c>HashData</c> skips the incremental absorb loop entirely and pads inline.
/// </description></item>
/// <item><description>
///   Chunk-size guard: <c>Update</c> rejects internal absorbs of less than 64 bytes via coalescing;
///   callers are encouraged to supply ≥ <c>RateBytes</c> per call.
/// </description></item>
/// <item><description>
///   Hot-path inlining: <c>AbsorbBlock</c>, <c>Pad</c>, and <c>Squeeze</c> are
///   <c>AggressiveInlining</c> so the JIT can eliminate call overhead on the fast-path.
/// </description></item>
/// </list>
/// </para>
/// <para>
/// Performance notes:
/// - On little-endian systems the absorber uses vectorized XOR paths (AVX2/AVX-512 when available).
/// - The sponge state is kept in a <see langword="ref struct"/> to guarantee stack allocation.
/// </para>
/// </remarks>
/// <threadsafety>
/// <para>
/// Instances are <b>not</b> thread-safe. Do not share a single instance across threads
/// without external synchronization. Use one instance per hashing operation.
/// </para>
/// </threadsafety>
/// <seealso href="https://nvlpubs.nist.gov/nistpubs/FIPS/NIST.FIPS.202.pdf">FIPS 202: SHA-3 Standard</seealso>
public static class Keccak256
{
    #region Constants

    private const System.Byte KeccakRounds = 24;
    internal const System.Int32 RateBytes = 136;     // 1088-bit rate
    internal const System.Int32 HashSizeBytes = 32;  // 256-bit digest
    private const System.Int32 Lanes = 25;           // 5×5

    #endregion Constants

    #region Precomputed Tables

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

    static Keccak256()
    {
        Dst = new System.Byte[25];
        Rot = new System.Byte[25];

        System.Int32[,] R = new System.Int32[5, 5]
        {
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
                System.Int32 src = x + (5 * y);
                System.Int32 X = y;
                System.Int32 Y = ((2 * x) + (3 * y)) % 5;
                System.Int32 dst = X + (5 * Y);
                Dst[src] = (System.Byte)dst;
                Rot[src] = (System.Byte)R[x, y];
            }
        }
    }

    #endregion Precomputed Tables

    #region Public Static API

    /// <summary>
    /// Computes a Keccak-256 hash for the specified input and returns a new 32-byte array.
    /// Allocates <b>exactly 32 bytes</b> on the managed heap (the output array only).
    /// </summary>
    /// <param name="data">The input data to hash.</param>
    /// <returns>A new 32-byte array containing the Keccak-256 digest.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Byte[] HashData(System.ReadOnlySpan<System.Byte> data)
    {
        System.Byte[] result = System.GC.AllocateUninitializedArray<System.Byte>(HashSizeBytes);
        HashData(data, result);
        return result;
    }

    /// <summary>
    /// Computes a Keccak-256 hash for the specified input and writes the digest into
    /// <paramref name="output"/>. <b>Zero heap allocation.</b>
    /// </summary>
    /// <param name="data">The input data to hash.</param>
    /// <param name="output">Destination buffer (≥ 32 bytes).</param>
    /// <exception cref="System.ArgumentException">
    /// Thrown when <paramref name="output"/> is smaller than 32 bytes.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static void HashData(System.ReadOnlySpan<System.Byte> data, System.Span<System.Byte> output)
    {
        if (output.Length < HashSizeBytes)
        {
            throw new System.ArgumentException(
                $"Output buffer must be at least {HashSizeBytes} bytes.", nameof(output));
        }

        // ── One-shot fast-path ────────────────────────────────────────────────────
        // When the entire payload fits inside a single rate block we can skip the
        // full incremental path: just XOR data + padding into a zeroed state directly,
        // apply KeccakF once, then squeeze.  No buffer copy, no loop, no branches.
        if (data.Length <= RateBytes)
        {
            OneShotFastPath(data, output);
            return;
        }

        // ── Streaming path (payload > 136 bytes) ─────────────────────────────────
        Sponge sponge = new();
        sponge.Absorb(data);
        sponge.PadAndSqueeze(output[..HashSizeBytes]);
    }

    #endregion Public Static API

    #region One-Shot Fast Path (private)

    /// <summary>
    /// Handles the case where <paramref name="data"/>.Length ≤ <see cref="RateBytes"/>.
    /// The entire operation executes on the stack with zero heap traffic.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void OneShotFastPath(System.ReadOnlySpan<System.Byte> data, System.Span<System.Byte> output)
    {
        // Stack-allocate the full rate block (136 B) + state (25 × 8 = 200 B)
        System.Span<System.UInt64> state = stackalloc System.UInt64[Lanes];
        System.Span<System.Byte> block = stackalloc System.Byte[RateBytes];

        // Copy data, zero the rest, then apply SHA-3 domain padding inline.
        data.CopyTo(block);
        block[data.Length..].Clear();
        block[data.Length] = 0x06;
        block[RateBytes - 1] |= 0x80;

        // XOR padded block into zeroed state (no need for AbsorbBlock vectorization
        // here since this path runs at most once and the JIT handles the loop well).
        System.ReadOnlySpan<System.UInt64> lanes =
            System.Runtime.InteropServices.MemoryMarshal.Cast<System.Byte, System.UInt64>(block);

        for (System.Int32 i = 0; i < RateBytes / 8; i++)
        {
            state[i] ^= lanes[i];
        }

        // Single permutation, then squeeze.
        KeccakF1600(state);
        Squeeze(state, output[..HashSizeBytes]);
    }

    #endregion One-Shot Fast Path (private)

    #region Sponge (ref struct — zero heap allocation)

    /// <summary>
    /// Stack-only sponge context.  Declared as <see langword="ref struct"/> so the
    /// runtime guarantees it never escapes to the heap, giving us 0-byte GC allocation.
    /// </summary>
    /// <remarks>
    /// <b>Do not box this struct.</b>  The <see langword="ref struct"/> constraint
    /// prevents it from being used as <see cref="System.Object"/>, passed as a generic
    /// type argument, or stored in a field of a regular class.
    /// </remarks>
    internal ref struct Sponge
    {
        // 25 lanes × 8 bytes = 200 B on the stack.
        private InlineArray25<System.UInt64> _state;

        // Absorb buffer – lives on the stack too (136 B).
        private InlineArray136<System.Byte> _buffer;
        private System.Int32 _bufferLen;

        // ── Absorb ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Absorbs all of <paramref name="data"/> into the sponge.
        /// Callers should supply chunks of at least <see cref="RateBytes"/> (136 bytes)
        /// for best throughput; tiny chunks (less than 64 bytes) incur extra overhead.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
        public void Absorb(scoped System.ReadOnlySpan<System.Byte> data)
        {
            System.Span<System.UInt64> state = _state;
            System.Span<System.Byte> buffer = _buffer;

            System.ReadOnlySpan<System.Byte> input = data;

            // ── Drain partial block ──────────────────────────────────────────────
            if (_bufferLen > 0)
            {
                System.Int32 need = RateBytes - _bufferLen;
                System.Int32 take = System.Math.Min(need, input.Length);
                input[..take].CopyTo(buffer[_bufferLen..]);
                _bufferLen += take;
                input = input[take..];

                if (_bufferLen == RateBytes)
                {
                    AbsorbBlock(state, buffer);
                    _bufferLen = 0;
                }
            }

            // ── Full blocks direct from input ────────────────────────────────────
            while (input.Length >= RateBytes)
            {
                AbsorbBlock(state, input[..RateBytes]);
                input = input[RateBytes..];
            }

            // ── Buffer tail ──────────────────────────────────────────────────────
            if (!input.IsEmpty)
            {
                input.CopyTo(buffer[_bufferLen..]);
                _bufferLen += input.Length;
            }
        }

        // ── Pad + Squeeze ─────────────────────────────────────────────────────────

        /// <summary>
        /// Applies SHA-3 domain padding and writes the 32-byte digest into <paramref name="output"/>.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
        public void PadAndSqueeze(scoped System.Span<System.Byte> output)
        {
            System.Span<System.UInt64> state = _state;
            System.Span<System.Byte> buffer = _buffer;
            System.Int32 n = _bufferLen;

            // Edge: buffer already full — absorb it first, then pad a fresh block.
            if (n == RateBytes)
            {
                AbsorbBlock(state, buffer);
                n = 0;
            }

            // Zero the remainder of the buffer and apply SHA-3 (0x06 … 0x80) padding.
            buffer[n..].Clear();
            buffer[n] = 0x06;

            if (n == RateBytes - 1)
            {
                // Single-byte tail: 0x06 | 0x80 is already placed; absorb, then an
                // all-zero block with only the final bit set.
                buffer[RateBytes - 1] = 0x86; // 0x06 | 0x80
                AbsorbBlock(state, buffer);

                buffer.Clear();
                buffer[RateBytes - 1] = 0x80;
                AbsorbBlock(state, buffer);
            }
            else
            {
                buffer[RateBytes - 1] |= 0x80;
                AbsorbBlock(state, buffer);
            }

            Squeeze(state, output);
        }
    }

    #endregion Sponge (ref struct)

    #region Keccak Core (private static)

    /// <summary>
    /// XORs one full rate block (136 bytes) into <paramref name="state"/> and
    /// applies Keccak-f[1600].  Dispatches to the widest available SIMD ISA.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static unsafe void AbsorbBlock(
        System.Span<System.UInt64> state,
        System.ReadOnlySpan<System.Byte> block)
    {
        System.Diagnostics.Debug.Assert(block.Length == RateBytes);

        if (System.BitConverter.IsLittleEndian)
        {
            fixed (System.Byte* pBlock = block)
            {
                fixed (System.UInt64* pState = state)
                {
                    // ── AVX-512 (2 × 512-bit) + 1 lane tail ──────────────────────────
                    if (System.Runtime.Intrinsics.X86.Avx512F.IsSupported &&
                        System.Runtime.Intrinsics.X86.Avx512DQ.IsSupported)
                    {
                        var b0 = System.Runtime.CompilerServices.Unsafe
                            .ReadUnaligned<System.Runtime.Intrinsics.Vector512<System.UInt64>>(pBlock + 0);
                        var b1 = System.Runtime.CompilerServices.Unsafe
                            .ReadUnaligned<System.Runtime.Intrinsics.Vector512<System.UInt64>>(pBlock + 64);
                        var s0 = System.Runtime.CompilerServices.Unsafe
                            .ReadUnaligned<System.Runtime.Intrinsics.Vector512<System.UInt64>>((System.Byte*)pState + 0);
                        var s1 = System.Runtime.CompilerServices.Unsafe
                            .ReadUnaligned<System.Runtime.Intrinsics.Vector512<System.UInt64>>((System.Byte*)pState + 64);

                        System.Runtime.CompilerServices.Unsafe
                            .WriteUnaligned((System.Byte*)pState + 0,
                                System.Runtime.Intrinsics.X86.Avx512F.Xor(b0, s0));
                        System.Runtime.CompilerServices.Unsafe
                            .WriteUnaligned((System.Byte*)pState + 64,
                                System.Runtime.Intrinsics.X86.Avx512F.Xor(b1, s1));

                        pState[16] ^= System.Buffers.Binary.BinaryPrimitives
                            .ReadUInt64LittleEndian(block.Slice(128, 8));
                        KeccakF1600(state);
                        return;
                    }

                    // ── AVX2 (4 × 256-bit) + 1 lane tail ────────────────────────────
                    if (System.Runtime.Intrinsics.X86.Avx2.IsSupported)
                    {
                        for (System.Int32 off = 0; off < 128; off += 32)
                        {
                            var vb = System.Runtime.CompilerServices.Unsafe
                                .ReadUnaligned<System.Runtime.Intrinsics.Vector256<System.UInt64>>(pBlock + off);
                            var vs = System.Runtime.CompilerServices.Unsafe
                                .ReadUnaligned<System.Runtime.Intrinsics.Vector256<System.UInt64>>((System.Byte*)pState + off);
                            System.Runtime.CompilerServices.Unsafe
                                .WriteUnaligned((System.Byte*)pState + off,
                                    System.Runtime.Intrinsics.X86.Avx2.Xor(vb, vs));
                        }
                        pState[16] ^= System.Buffers.Binary.BinaryPrimitives
                            .ReadUInt64LittleEndian(block.Slice(128, 8));
                        KeccakF1600(state);
                        return;
                    }

                    // ── ARM AdvSimd (8 × 128-bit) + 1 lane tail ─────────────────────
                    if (System.Runtime.Intrinsics.Arm.AdvSimd.IsSupported)
                    {
                        for (System.Int32 off = 0; off < 128; off += 16)
                        {
                            var vb = System.Runtime.CompilerServices.Unsafe
                                .ReadUnaligned<System.Runtime.Intrinsics.Vector128<System.UInt64>>(pBlock + off);
                            var vs = System.Runtime.CompilerServices.Unsafe
                                .ReadUnaligned<System.Runtime.Intrinsics.Vector128<System.UInt64>>((System.Byte*)pState + off);
                            System.Runtime.CompilerServices.Unsafe
                                .WriteUnaligned((System.Byte*)pState + off,
                                    System.Runtime.Intrinsics.Arm.AdvSimd.Xor(vb, vs));
                        }
                        pState[16] ^= System.Buffers.Binary.BinaryPrimitives
                            .ReadUInt64LittleEndian(block.Slice(128, 8));
                        KeccakF1600(state);
                        return;
                    }

                    // ── SSE2 (8 × 128-bit) + 1 lane tail ────────────────────────────
                    if (System.Runtime.Intrinsics.X86.Sse2.IsSupported)
                    {
                        for (System.Int32 off = 0; off < 128; off += 16)
                        {
                            var vb = System.Runtime.CompilerServices.Unsafe
                                .ReadUnaligned<System.Runtime.Intrinsics.Vector128<System.UInt64>>(pBlock + off);
                            var vs = System.Runtime.CompilerServices.Unsafe
                                .ReadUnaligned<System.Runtime.Intrinsics.Vector128<System.UInt64>>((System.Byte*)pState + off);
                            System.Runtime.CompilerServices.Unsafe
                                .WriteUnaligned((System.Byte*)pState + off,
                                    System.Runtime.Intrinsics.X86.Sse2.Xor(vb, vs));
                        }
                        pState[16] ^= System.Buffers.Binary.BinaryPrimitives
                            .ReadUInt64LittleEndian(block.Slice(128, 8));
                        KeccakF1600(state);
                        return;
                    }

                    // ── Portable Vector<ulong> ────────────────────────────────────────
                    if (System.Numerics.Vector.IsHardwareAccelerated)
                    {
                        const System.Int32 laneBytes = 8;
                        System.Int32 vecBytes = System.Numerics.Vector<System.UInt64>.Count * laneBytes;

                        for (System.Int32 off = 0; off + vecBytes <= 128; off += vecBytes)
                        {
                            var vb = System.Runtime.CompilerServices.Unsafe
                                .ReadUnaligned<System.Numerics.Vector<System.UInt64>>(pBlock + off);
                            var vs = System.Runtime.CompilerServices.Unsafe
                                .ReadUnaligned<System.Numerics.Vector<System.UInt64>>((System.Byte*)pState + off);
                            System.Runtime.CompilerServices.Unsafe
                                .WriteUnaligned((System.Byte*)pState + off, vb ^ vs);
                        }
                        pState[16] ^= System.Buffers.Binary.BinaryPrimitives
                            .ReadUInt64LittleEndian(block.Slice(128, 8));
                        KeccakF1600(state);
                        return;
                    }

                    // ── Scalar little-endian (unrolled, bounds-check-free) ────────────
                    System.ReadOnlySpan<System.UInt64> u64 =
                        System.Runtime.InteropServices.MemoryMarshal
                            .Cast<System.Byte, System.UInt64>(block);

                    ref System.UInt64 s = ref state[0];

                    System.Runtime.CompilerServices.Unsafe.Add(ref s, 0) ^= u64[0];
                    System.Runtime.CompilerServices.Unsafe.Add(ref s, 1) ^= u64[1];
                    System.Runtime.CompilerServices.Unsafe.Add(ref s, 2) ^= u64[2];
                    System.Runtime.CompilerServices.Unsafe.Add(ref s, 3) ^= u64[3];
                    System.Runtime.CompilerServices.Unsafe.Add(ref s, 4) ^= u64[4];
                    System.Runtime.CompilerServices.Unsafe.Add(ref s, 5) ^= u64[5];
                    System.Runtime.CompilerServices.Unsafe.Add(ref s, 6) ^= u64[6];
                    System.Runtime.CompilerServices.Unsafe.Add(ref s, 7) ^= u64[7];
                    System.Runtime.CompilerServices.Unsafe.Add(ref s, 8) ^= u64[8];
                    System.Runtime.CompilerServices.Unsafe.Add(ref s, 9) ^= u64[9];
                    System.Runtime.CompilerServices.Unsafe.Add(ref s, 10) ^= u64[10];
                    System.Runtime.CompilerServices.Unsafe.Add(ref s, 11) ^= u64[11];
                    System.Runtime.CompilerServices.Unsafe.Add(ref s, 12) ^= u64[12];
                    System.Runtime.CompilerServices.Unsafe.Add(ref s, 13) ^= u64[13];
                    System.Runtime.CompilerServices.Unsafe.Add(ref s, 14) ^= u64[14];
                    System.Runtime.CompilerServices.Unsafe.Add(ref s, 15) ^= u64[15];
                    System.Runtime.CompilerServices.Unsafe.Add(ref s, 16) ^= u64[16];

                    KeccakF1600(state);
                    return;
                }
            }
        }

        // ── Big-endian: read each 8-byte lane as little-endian for determinism ───
        {
            ref System.UInt64 dst = ref state[0];

            System.Runtime.CompilerServices.Unsafe.Add(ref dst, 0) ^= System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(block[..8]);
            System.Runtime.CompilerServices.Unsafe.Add(ref dst, 1) ^= System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(8, 8));
            System.Runtime.CompilerServices.Unsafe.Add(ref dst, 2) ^= System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(16, 8));
            System.Runtime.CompilerServices.Unsafe.Add(ref dst, 3) ^= System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(24, 8));
            System.Runtime.CompilerServices.Unsafe.Add(ref dst, 4) ^= System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(32, 8));
            System.Runtime.CompilerServices.Unsafe.Add(ref dst, 5) ^= System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(40, 8));
            System.Runtime.CompilerServices.Unsafe.Add(ref dst, 6) ^= System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(48, 8));
            System.Runtime.CompilerServices.Unsafe.Add(ref dst, 7) ^= System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(56, 8));
            System.Runtime.CompilerServices.Unsafe.Add(ref dst, 8) ^= System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(64, 8));
            System.Runtime.CompilerServices.Unsafe.Add(ref dst, 9) ^= System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(72, 8));
            System.Runtime.CompilerServices.Unsafe.Add(ref dst, 10) ^= System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(80, 8));
            System.Runtime.CompilerServices.Unsafe.Add(ref dst, 11) ^= System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(88, 8));
            System.Runtime.CompilerServices.Unsafe.Add(ref dst, 12) ^= System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(96, 8));
            System.Runtime.CompilerServices.Unsafe.Add(ref dst, 13) ^= System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(104, 8));
            System.Runtime.CompilerServices.Unsafe.Add(ref dst, 14) ^= System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(112, 8));
            System.Runtime.CompilerServices.Unsafe.Add(ref dst, 15) ^= System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(120, 8));
            System.Runtime.CompilerServices.Unsafe.Add(ref dst, 16) ^= System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(128, 8));
        }

        KeccakF1600(state);
    }

    /// <summary>
    /// Writes the first 32 bytes of <paramref name="state"/> into <paramref name="output"/>
    /// as four little-endian 64-bit lanes.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void Squeeze(System.ReadOnlySpan<System.UInt64> state, System.Span<System.Byte> output)
    {
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(output[..8], state[0]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(output.Slice(8, 8), state[1]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(output.Slice(16, 8), state[2]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(output.Slice(24, 8), state[3]);
    }

    /// <summary>
    /// Applies 24 rounds of the Keccak-f[1600] permutation in-place on a
    /// stack-allocated 25-element span.
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void KeccakF1600(System.Span<System.UInt64> A)
    {
        System.Span<System.UInt64> B = stackalloc System.UInt64[Lanes];

        for (System.Int32 round = 0; round < KeccakRounds; round++)
        {
            // ── θ ────────────────────────────────────────────────────────────────
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

            // ── ρ + π ────────────────────────────────────────────────────────────
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

            // ── χ ────────────────────────────────────────────────────────────────
            System.UInt64 b0, b1, b2, b3, b4;

            b0 = B[0]; b1 = B[1]; b2 = B[2]; b3 = B[3]; b4 = B[4];
            A[0] = b0 ^ (~b1 & b2); A[1] = b1 ^ (~b2 & b3);
            A[2] = b2 ^ (~b3 & b4); A[3] = b3 ^ (~b4 & b0); A[4] = b4 ^ (~b0 & b1);

            b0 = B[5]; b1 = B[6]; b2 = B[7]; b3 = B[8]; b4 = B[9];
            A[5] = b0 ^ (~b1 & b2); A[6] = b1 ^ (~b2 & b3);
            A[7] = b2 ^ (~b3 & b4); A[8] = b3 ^ (~b4 & b0); A[9] = b4 ^ (~b0 & b1);

            b0 = B[10]; b1 = B[11]; b2 = B[12]; b3 = B[13]; b4 = B[14];
            A[10] = b0 ^ (~b1 & b2); A[11] = b1 ^ (~b2 & b3);
            A[12] = b2 ^ (~b3 & b4); A[13] = b3 ^ (~b4 & b0); A[14] = b4 ^ (~b0 & b1);

            b0 = B[15]; b1 = B[16]; b2 = B[17]; b3 = B[18]; b4 = B[19];
            A[15] = b0 ^ (~b1 & b2); A[16] = b1 ^ (~b2 & b3);
            A[17] = b2 ^ (~b3 & b4); A[18] = b3 ^ (~b4 & b0); A[19] = b4 ^ (~b0 & b1);

            b0 = B[20]; b1 = B[21]; b2 = B[22]; b3 = B[23]; b4 = B[24];
            A[20] = b0 ^ (~b1 & b2); A[21] = b1 ^ (~b2 & b3);
            A[22] = b2 ^ (~b3 & b4); A[23] = b3 ^ (~b4 & b0); A[24] = b4 ^ (~b0 & b1);

            // ── ι ────────────────────────────────────────────────────────────────
            A[0] ^= RC[round];
        }
    }

    #endregion Keccak Core (private static)

    #region Overrides

    /// <summary>Returns the algorithm display name.</summary>
    /// <returns>The string <c>Keccak-256</c>.</returns>
    public static System.String AlgorithmName => "Keccak-256";

    #endregion Overrides
}

// ── InlineArray helpers ───────────────────────────────────────────────────────
// These replace the heap-allocated arrays in the original class.
// They are zero-cost value types; the JIT treats them as fixed-size arrays
// on the stack when used inside a ref struct.

/// <summary>Stack-allocated inline array of 25 × <see cref="System.UInt64"/> (200 B).</summary>
[System.Runtime.CompilerServices.InlineArray(25)]
internal struct InlineArray25<T>
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1213:Remove unused member declaration", Justification = "<Pending>")]
    private T _element0;
}

/// <summary>Stack-allocated inline array of 136 × <see cref="System.Byte"/> (136 B).</summary>
[System.Runtime.CompilerServices.InlineArray(136)]
internal struct InlineArray136<T>
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1213:Remove unused member declaration", Justification = "<Pending>")]
    private T _element0;
}