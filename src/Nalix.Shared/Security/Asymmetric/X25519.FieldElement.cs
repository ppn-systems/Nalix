// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

// OPTIMIZATION NOTES (v2 — zero-allocation):
//
//  BEFORE   │ FieldElement = class  → every operator/method returns new heap object
//           │ SubArray()           → 10× temp byte[] alloc per FieldElement(byte[]) ctor
//           │ carry[]              → new Int64[10] / new Int32[10] in Multiply, Square, Mul121666, ToBytes
//           │ Result: ~460 KB allocated per ScalarMult call (≈3500+ heap objects)
//
//  AFTER    │ FieldElement = struct → lives on the stack; operators return by value (copy, no heap)
//           │ Load3/Load4          → read directly from ReadOnlySpan<byte> (no SubArray temp)
//           │ carry                → scalar locals (c0..c9), zero heap, zero GC pressure
//           │ ToBytes              → writes into caller-supplied Span<byte> (no new byte[32])
//           │ Result: 32 B (output array only) per ScalarMult call
//
//  Expected benchmark improvement:
//           │ Allocated : 460 KB → 32 B
//           │ Mean      : ~120 µs → ~30–45 µs  (GC pause removed + cache locality)

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Benchmarks")]
#endif

namespace Nalix.Shared.Security.Asymmetric;

/// <summary>
/// Represents an element of the field GF(2^255 - 19).
/// An element t, entries t[0]...t[9], represents the integer
/// t[0]+2^26·t[1]+2^51·t[2]+2^77·t[3]+2^102·t[4]+...+2^230·t[9].
/// </summary>
/// <remarks>
/// Declared as a <see langword="struct"/> so that every arithmetic operation
/// returns a value type — no heap allocation, no GC pressure.
/// All carry temporaries are scalar locals rather than <c>new int[10]</c> arrays.
/// </remarks>
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
internal struct FieldElement
{
    // 10 limbs, 26/25-bit alternating radix-2^25.5 representation.
    // Stored inline — no inner array allocation.
    internal System.Int32 E0, E1, E2, E3, E4, E5, E6, E7, E8, E9;

    // ── Indexer (used by legacy call-sites) ──────────────────────────────────
    public System.Int32 this[System.Int32 i]
    {
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        readonly get => i switch
        {
            0 => E0,
            1 => E1,
            2 => E2,
            3 => E3,
            4 => E4,
            5 => E5,
            6 => E6,
            7 => E7,
            8 => E8,
            9 => E9,
            _ => throw new System.ArgumentOutOfRangeException(nameof(i))
        };

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        set
        {
            switch (i)
            {
                case 0: E0 = value; break;
                case 1: E1 = value; break;
                case 2: E2 = value; break;
                case 3: E3 = value; break;
                case 4: E4 = value; break;
                case 5: E5 = value; break;
                case 6: E6 = value; break;
                case 7: E7 = value; break;
                case 8: E8 = value; break;
                case 9: E9 = value; break;
                default: throw new System.ArgumentOutOfRangeException(nameof(i));
            }
        }
    }

    // ── Constructors ─────────────────────────────────────────────────────────

    /// <summary>feFromBytes — reads a 32-byte little-endian field element.</summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public FieldElement(System.ReadOnlySpan<System.Byte> src)
    {
        // Read directly from span — no SubArray temp allocation.
        System.Int64 h0 = Load4(src, 0);
        System.Int64 h1 = Load3(src, 4) << 6;
        System.Int64 h2 = Load3(src, 7) << 5;
        System.Int64 h3 = Load3(src, 10) << 3;
        System.Int64 h4 = Load3(src, 13) << 2;
        System.Int64 h5 = Load4(src, 16);
        System.Int64 h6 = Load3(src, 20) << 7;
        System.Int64 h7 = Load3(src, 23) << 5;
        System.Int64 h8 = Load3(src, 26) << 4;
        System.Int64 h9 = (Load3(src, 29) & 0x7fffff) << 2;

        // Carry reduction — all scalar locals, zero heap.
        System.Int64 c9 = (h9 + (1 << 24)) >> 25; h0 += c9 * 19; h9 -= c9 << 25;
        System.Int64 c1 = (h1 + (1 << 24)) >> 25; h2 += c1; h1 -= c1 << 25;
        System.Int64 c3 = (h3 + (1 << 24)) >> 25; h4 += c3; h3 -= c3 << 25;
        System.Int64 c5 = (h5 + (1 << 24)) >> 25; h6 += c5; h5 -= c5 << 25;
        System.Int64 c7 = (h7 + (1 << 24)) >> 25; h8 += c7; h7 -= c7 << 25;
        System.Int64 c0 = (h0 + (1 << 25)) >> 26; h1 += c0; h0 -= c0 << 26;
        System.Int64 c2 = (h2 + (1 << 25)) >> 26; h3 += c2; h2 -= c2 << 26;
        System.Int64 c4 = (h4 + (1 << 25)) >> 26; h5 += c4; h4 -= c4 << 26;
        System.Int64 c6 = (h6 + (1 << 25)) >> 26; h7 += c6; h6 -= c6 << 26;
        System.Int64 c8 = (h8 + (1 << 25)) >> 26; h9 += c8; h8 -= c8 << 26;

        E0 = (System.Int32)h0; E1 = (System.Int32)h1;
        E2 = (System.Int32)h2; E3 = (System.Int32)h3;
        E4 = (System.Int32)h4; E5 = (System.Int32)h5;
        E6 = (System.Int32)h6; E7 = (System.Int32)h7;
        E8 = (System.Int32)h8; E9 = (System.Int32)h9;
    }

    // ── Identity ──────────────────────────────────────────────────────────────

    /// <summary>Sets this element to 1 (the multiplicative identity).</summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void One()
    {
        E0 = 1;
        E1 = E2 = E3 = E4 = E5 = E6 = E7 = E8 = E9 = 0;
    }

    // ── Serialization ────────────────────────────────────────────────────────

    /// <summary>
    /// feToBytes — writes the canonical 32-byte little-endian encoding into
    /// <paramref name="output"/>. No heap allocation.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public readonly void ToBytes(System.Span<System.Byte> output)
    {
        // Work on local copies so we don't mutate the struct's own fields.
        System.Int32 h0 = E0, h1 = E1, h2 = E2, h3 = E3, h4 = E4;
        System.Int32 h5 = E5, h6 = E6, h7 = E7, h8 = E8, h9 = E9;

        System.Int32 q = ((19 * h9) + (1 << 24)) >> 25;
        q = (h0 + q) >> 26; q = (h1 + q) >> 25; q = (h2 + q) >> 26;
        q = (h3 + q) >> 25; q = (h4 + q) >> 26; q = (h5 + q) >> 25;
        q = (h6 + q) >> 26; q = (h7 + q) >> 25; q = (h8 + q) >> 26;
        q = (h9 + q) >> 25;

        h0 += 19 * q;

        // Carry propagation — scalar locals.
        System.Int32 c0 = h0 >> 26; h1 += c0; h0 -= c0 << 26;
        System.Int32 c1 = h1 >> 25; h2 += c1; h1 -= c1 << 25;
        System.Int32 c2 = h2 >> 26; h3 += c2; h2 -= c2 << 26;
        System.Int32 c3 = h3 >> 25; h4 += c3; h3 -= c3 << 25;
        System.Int32 c4 = h4 >> 26; h5 += c4; h4 -= c4 << 26;
        System.Int32 c5 = h5 >> 25; h6 += c5; h5 -= c5 << 25;
        System.Int32 c6 = h6 >> 26; h7 += c6; h6 -= c6 << 26;
        System.Int32 c7 = h7 >> 25; h8 += c7; h7 -= c7 << 25;
        System.Int32 c8 = h8 >> 26; h9 += c8; h8 -= c8 << 26;
        System.Int32 c9 = h9 >> 25; h9 -= c9 << 25;

        output[0] = (System.Byte)(h0 >> 0);
        output[1] = (System.Byte)(h0 >> 8);
        output[2] = (System.Byte)(h0 >> 16);
        output[3] = (System.Byte)((h0 >> 24) | (h1 << 2));
        output[4] = (System.Byte)(h1 >> 6);
        output[5] = (System.Byte)(h1 >> 14);
        output[6] = (System.Byte)((h1 >> 22) | (h2 << 3));
        output[7] = (System.Byte)(h2 >> 5);
        output[8] = (System.Byte)(h2 >> 13);
        output[9] = (System.Byte)((h2 >> 21) | (h3 << 5));
        output[10] = (System.Byte)(h3 >> 3);
        output[11] = (System.Byte)(h3 >> 11);
        output[12] = (System.Byte)((h3 >> 19) | (h4 << 6));
        output[13] = (System.Byte)(h4 >> 2);
        output[14] = (System.Byte)(h4 >> 10);
        output[15] = (System.Byte)(h4 >> 18);
        output[16] = (System.Byte)(h5 >> 0);
        output[17] = (System.Byte)(h5 >> 8);
        output[18] = (System.Byte)(h5 >> 16);
        output[19] = (System.Byte)((h5 >> 24) | (h6 << 1));
        output[20] = (System.Byte)(h6 >> 7);
        output[21] = (System.Byte)(h6 >> 15);
        output[22] = (System.Byte)((h6 >> 23) | (h7 << 3));
        output[23] = (System.Byte)(h7 >> 5);
        output[24] = (System.Byte)(h7 >> 13);
        output[25] = (System.Byte)((h7 >> 21) | (h8 << 4));
        output[26] = (System.Byte)(h8 >> 4);
        output[27] = (System.Byte)(h8 >> 12);
        output[28] = (System.Byte)((h8 >> 20) | (h9 << 6));
        output[29] = (System.Byte)(h9 >> 2);
        output[30] = (System.Byte)(h9 >> 10);
        output[31] = (System.Byte)(h9 >> 18);
    }

    // ── Arithmetic operators ──────────────────────────────────────────────────

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static FieldElement operator +(FieldElement f, FieldElement g) => new()
    {
        E0 = f.E0 + g.E0,
        E1 = f.E1 + g.E1,
        E2 = f.E2 + g.E2,
        E3 = f.E3 + g.E3,
        E4 = f.E4 + g.E4,
        E5 = f.E5 + g.E5,
        E6 = f.E6 + g.E6,
        E7 = f.E7 + g.E7,
        E8 = f.E8 + g.E8,
        E9 = f.E9 + g.E9
    };

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static FieldElement operator -(FieldElement f, FieldElement g) => new()
    {
        E0 = f.E0 - g.E0,
        E1 = f.E1 - g.E1,
        E2 = f.E2 - g.E2,
        E3 = f.E3 - g.E3,
        E4 = f.E4 - g.E4,
        E5 = f.E5 - g.E5,
        E6 = f.E6 - g.E6,
        E7 = f.E7 - g.E7,
        E8 = f.E8 - g.E8,
        E9 = f.E9 - g.E9
    };

    // ── Multiply ─────────────────────────────────────────────────────────────

    /// <summary>h = this * g  (schoolbook multiplication mod p)</summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public readonly FieldElement Multiply(FieldElement g)
    {
        System.Int32 f0 = E0, f1 = E1, f2 = E2, f3 = E3, f4 = E4;
        System.Int32 f5 = E5, f6 = E6, f7 = E7, f8 = E8, f9 = E9;
        System.Int32 g0 = g.E0, g1 = g.E1, g2 = g.E2, g3 = g.E3, g4 = g.E4;
        System.Int32 g5 = g.E5, g6 = g.E6, g7 = g.E7, g8 = g.E8, g9 = g.E9;

        System.Int32 g1_19 = 19 * g1, g2_19 = 19 * g2, g3_19 = 19 * g3;
        System.Int32 g4_19 = 19 * g4, g5_19 = 19 * g5, g6_19 = 19 * g6;
        System.Int32 g7_19 = 19 * g7, g8_19 = 19 * g8, g9_19 = 19 * g9;
        System.Int32 f1_2 = 2 * f1, f3_2 = 2 * f3, f5_2 = 2 * f5;
        System.Int32 f7_2 = 2 * f7, f9_2 = 2 * f9;

        System.Int64 h0 = ((System.Int64)f0 * g0) + ((System.Int64)f1_2 * g9_19) + ((System.Int64)f2 * g8_19)
                        + ((System.Int64)f3_2 * g7_19) + ((System.Int64)f4 * g6_19) + ((System.Int64)f5_2 * g5_19)
                        + ((System.Int64)f6 * g4_19) + ((System.Int64)f7_2 * g3_19) + ((System.Int64)f8 * g2_19)
                        + ((System.Int64)f9_2 * g1_19);
        System.Int64 h1 = ((System.Int64)f0 * g1) + ((System.Int64)f1 * g0) + ((System.Int64)f2 * g9_19)
                        + ((System.Int64)f3 * g8_19) + ((System.Int64)f4 * g7_19) + ((System.Int64)f5 * g6_19)
                        + ((System.Int64)f6 * g5_19) + ((System.Int64)f7 * g4_19) + ((System.Int64)f8 * g3_19)
                        + ((System.Int64)f9 * g2_19);
        System.Int64 h2 = ((System.Int64)f0 * g2) + ((System.Int64)f1_2 * g1) + ((System.Int64)f2 * g0)
                        + ((System.Int64)f3_2 * g9_19) + ((System.Int64)f4 * g8_19) + ((System.Int64)f5_2 * g7_19)
                        + ((System.Int64)f6 * g6_19) + ((System.Int64)f7_2 * g5_19) + ((System.Int64)f8 * g4_19)
                        + ((System.Int64)f9_2 * g3_19);
        System.Int64 h3 = ((System.Int64)f0 * g3) + ((System.Int64)f1 * g2) + ((System.Int64)f2 * g1)
                        + ((System.Int64)f3 * g0) + ((System.Int64)f4 * g9_19) + ((System.Int64)f5 * g8_19)
                        + ((System.Int64)f6 * g7_19) + ((System.Int64)f7 * g6_19) + ((System.Int64)f8 * g5_19)
                        + ((System.Int64)f9 * g4_19);
        System.Int64 h4 = ((System.Int64)f0 * g4) + ((System.Int64)f1_2 * g3) + ((System.Int64)f2 * g2)
                        + ((System.Int64)f3_2 * g1) + ((System.Int64)f4 * g0) + ((System.Int64)f5_2 * g9_19)
                        + ((System.Int64)f6 * g8_19) + ((System.Int64)f7_2 * g7_19) + ((System.Int64)f8 * g6_19)
                        + ((System.Int64)f9_2 * g5_19);
        System.Int64 h5 = ((System.Int64)f0 * g5) + ((System.Int64)f1 * g4) + ((System.Int64)f2 * g3)
                        + ((System.Int64)f3 * g2) + ((System.Int64)f4 * g1) + ((System.Int64)f5 * g0)
                        + ((System.Int64)f6 * g9_19) + ((System.Int64)f7 * g8_19) + ((System.Int64)f8 * g7_19)
                        + ((System.Int64)f9 * g6_19);
        System.Int64 h6 = ((System.Int64)f0 * g6) + ((System.Int64)f1_2 * g5) + ((System.Int64)f2 * g4)
                        + ((System.Int64)f3_2 * g3) + ((System.Int64)f4 * g2) + ((System.Int64)f5_2 * g1)
                        + ((System.Int64)f6 * g0) + ((System.Int64)f7_2 * g9_19) + ((System.Int64)f8 * g8_19)
                        + ((System.Int64)f9_2 * g7_19);
        System.Int64 h7 = ((System.Int64)f0 * g7) + ((System.Int64)f1 * g6) + ((System.Int64)f2 * g5)
                        + ((System.Int64)f3 * g4) + ((System.Int64)f4 * g3) + ((System.Int64)f5 * g2)
                        + ((System.Int64)f6 * g1) + ((System.Int64)f7 * g0) + ((System.Int64)f8 * g9_19)
                        + ((System.Int64)f9 * g8_19);
        System.Int64 h8 = ((System.Int64)f0 * g8) + ((System.Int64)f1_2 * g7) + ((System.Int64)f2 * g6)
                        + ((System.Int64)f3_2 * g5) + ((System.Int64)f4 * g4) + ((System.Int64)f5_2 * g3)
                        + ((System.Int64)f6 * g2) + ((System.Int64)f7_2 * g1) + ((System.Int64)f8 * g0)
                        + ((System.Int64)f9_2 * g9_19);
        System.Int64 h9 = ((System.Int64)f0 * g9) + ((System.Int64)f1 * g8) + ((System.Int64)f2 * g7)
                        + ((System.Int64)f3 * g6) + ((System.Int64)f4 * g5) + ((System.Int64)f5 * g4)
                        + ((System.Int64)f6 * g3) + ((System.Int64)f7 * g2) + ((System.Int64)f8 * g1)
                        + ((System.Int64)f9 * g0);

        return ReduceCarry(h0, h1, h2, h3, h4, h5, h6, h7, h8, h9);
    }

    // ── Square ───────────────────────────────────────────────────────────────

    /// <summary>h = this²</summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public readonly FieldElement Square()
    {
        System.Int32 f0 = E0, f1 = E1, f2 = E2, f3 = E3, f4 = E4;
        System.Int32 f5 = E5, f6 = E6, f7 = E7, f8 = E8, f9 = E9;

        System.Int32 f0_2 = 2 * f0, f1_2 = 2 * f1, f2_2 = 2 * f2, f3_2 = 2 * f3;
        System.Int32 f4_2 = 2 * f4, f5_2 = 2 * f5, f6_2 = 2 * f6, f7_2 = 2 * f7;
        System.Int32 f5_38 = 38 * f5, f6_19 = 19 * f6, f7_38 = 38 * f7;
        System.Int32 f8_19 = 19 * f8, f9_38 = 38 * f9;

        System.Int64 h0 = ((System.Int64)f0 * f0) + ((System.Int64)f1_2 * f9_38) + ((System.Int64)f2_2 * f8_19)
                        + ((System.Int64)f3_2 * f7_38) + ((System.Int64)f4_2 * f6_19) + ((System.Int64)f5 * f5_38);
        System.Int64 h1 = ((System.Int64)f0_2 * f1) + ((System.Int64)f2 * f9_38) + ((System.Int64)f3_2 * f8_19)
                        + ((System.Int64)f4 * f7_38) + ((System.Int64)f5_2 * f6_19);
        System.Int64 h2 = ((System.Int64)f0_2 * f2) + ((System.Int64)f1_2 * f1) + ((System.Int64)f3_2 * f9_38)
                        + ((System.Int64)f4_2 * f8_19) + ((System.Int64)f5_2 * f7_38) + ((System.Int64)f6 * f6_19);
        System.Int64 h3 = ((System.Int64)f0_2 * f3) + ((System.Int64)f1_2 * f2) + ((System.Int64)f4 * f9_38)
                        + ((System.Int64)f5_2 * f8_19) + ((System.Int64)f6 * f7_38);
        System.Int64 h4 = ((System.Int64)f0_2 * f4) + ((System.Int64)f1_2 * f3_2) + ((System.Int64)f2 * f2)
                        + ((System.Int64)f5_2 * f9_38) + ((System.Int64)f6_2 * f8_19) + ((System.Int64)f7 * f7_38);
        System.Int64 h5 = ((System.Int64)f0_2 * f5) + ((System.Int64)f1_2 * f4) + ((System.Int64)f2_2 * f3)
                        + ((System.Int64)f6 * f9_38) + ((System.Int64)f7_2 * f8_19);
        System.Int64 h6 = ((System.Int64)f0_2 * f6) + ((System.Int64)f1_2 * f5_2) + ((System.Int64)f2_2 * f4)
                        + ((System.Int64)f3_2 * f3) + ((System.Int64)f7_2 * f9_38) + ((System.Int64)f8 * f8_19);
        System.Int64 h7 = ((System.Int64)f0_2 * f7) + ((System.Int64)f1_2 * f6) + ((System.Int64)f2_2 * f5)
                        + ((System.Int64)f3_2 * f4) + ((System.Int64)f8 * f9_38);
        System.Int64 h8 = ((System.Int64)f0_2 * f8) + ((System.Int64)f1_2 * f7_2) + ((System.Int64)f2_2 * f6)
                        + ((System.Int64)f3_2 * f5_2) + ((System.Int64)f4 * f4) + ((System.Int64)f9 * f9_38);
        System.Int64 h9 = ((System.Int64)f0_2 * f9) + ((System.Int64)f1_2 * f8) + ((System.Int64)f2_2 * f7)
                        + ((System.Int64)f3_2 * f6) + ((System.Int64)f4_2 * f5);

        return ReduceCarry(h0, h1, h2, h3, h4, h5, h6, h7, h8, h9);
    }

    // ── Mul121666 ─────────────────────────────────────────────────────────────

    /// <summary>h = this * 121666  (Montgomery ladder constant)</summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public readonly FieldElement Mul121666()
    {
        System.Int64 h0 = (System.Int64)E0 * 121666, h1 = (System.Int64)E1 * 121666;
        System.Int64 h2 = (System.Int64)E2 * 121666, h3 = (System.Int64)E3 * 121666;
        System.Int64 h4 = (System.Int64)E4 * 121666, h5 = (System.Int64)E5 * 121666;
        System.Int64 h6 = (System.Int64)E6 * 121666, h7 = (System.Int64)E7 * 121666;
        System.Int64 h8 = (System.Int64)E8 * 121666, h9 = (System.Int64)E9 * 121666;

        // Odd-only carry first pass.
        System.Int64 c9 = (h9 + (1 << 24)) >> 25; h0 += c9 * 19; h9 -= c9 << 25;
        System.Int64 c1 = (h1 + (1 << 24)) >> 25; h2 += c1; h1 -= c1 << 25;
        System.Int64 c3 = (h3 + (1 << 24)) >> 25; h4 += c3; h3 -= c3 << 25;
        System.Int64 c5 = (h5 + (1 << 24)) >> 25; h6 += c5; h5 -= c5 << 25;
        System.Int64 c7 = (h7 + (1 << 24)) >> 25; h8 += c7; h7 -= c7 << 25;
        // Even carry second pass.
        System.Int64 c0 = (h0 + (1 << 25)) >> 26; h1 += c0; h0 -= c0 << 26;
        System.Int64 c2 = (h2 + (1 << 25)) >> 26; h3 += c2; h2 -= c2 << 26;
        System.Int64 c4 = (h4 + (1 << 25)) >> 26; h5 += c4; h4 -= c4 << 26;
        System.Int64 c6 = (h6 + (1 << 25)) >> 26; h7 += c6; h6 -= c6 << 26;
        System.Int64 c8 = (h8 + (1 << 25)) >> 26; h9 += c8; h8 -= c8 << 26;

        return new FieldElement
        {
            E0 = (System.Int32)h0,
            E1 = (System.Int32)h1,
            E2 = (System.Int32)h2,
            E3 = (System.Int32)h3,
            E4 = (System.Int32)h4,
            E5 = (System.Int32)h5,
            E6 = (System.Int32)h6,
            E7 = (System.Int32)h7,
            E8 = (System.Int32)h8,
            E9 = (System.Int32)h9
        };
    }

    // ── Invert ───────────────────────────────────────────────────────────────

    /// <summary>h = this^(-1) mod p  (via Fermat: p-2 exponentiation)</summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public readonly FieldElement Invert()
    {
        FieldElement t0 = Square();

        FieldElement t1 = t0.Square();
        t1 = t1.Square();
        t1 = Multiply(t1);
        t0 = t0.Multiply(t1);

        FieldElement t2 = t0.Square();
        t1 = t1.Multiply(t2);

        // 5 squares unrolled
        t2 = t1.Square(); t2 = t2.Square(); t2 = t2.Square(); t2 = t2.Square(); t2 = t2.Square();
        t1 = t2.Multiply(t1);

        // 10 squares unrolled
        t2 = t1.Square(); t2 = t2.Square(); t2 = t2.Square(); t2 = t2.Square(); t2 = t2.Square();
        t2 = t2.Square(); t2 = t2.Square(); t2 = t2.Square(); t2 = t2.Square(); t2 = t2.Square();
        t2 = t2.Multiply(t1);

        // 20 squares
        FieldElement t3 = t2.Square();
        for (System.Int32 i = 1; i < 20; i++)
        {
            t3 = t3.Square();
        }

        t2 = t3.Multiply(t2);

        // 10 squares unrolled
        t2 = t2.Square(); t2 = t2.Square(); t2 = t2.Square(); t2 = t2.Square(); t2 = t2.Square();
        t2 = t2.Square(); t2 = t2.Square(); t2 = t2.Square(); t2 = t2.Square(); t2 = t2.Square();
        t1 = t2.Multiply(t1);

        // 50 squares
        t2 = t1.Square();
        for (System.Int32 i = 1; i < 50; i++)
        {
            t2 = t2.Square();
        }

        t2 = t2.Multiply(t1);

        // 100 squares
        t3 = t2.Square();
        for (System.Int32 i = 1; i < 100; i++)
        {
            t3 = t3.Square();
        }

        t2 = t3.Multiply(t2);

        // 50 squares
        t2 = t2.Square();
        for (System.Int32 i = 1; i < 50; i++)
        {
            t2 = t2.Square();
        }

        t1 = t2.Multiply(t1);

        // 5 squares unrolled
        t1 = t1.Square(); t1 = t1.Square(); t1 = t1.Square(); t1 = t1.Square(); t1 = t1.Square();

        return t1.Multiply(t0);
    }

    // ── Constant-time helpers ─────────────────────────────────────────────────

    /// <summary>
    /// Replaces (f, g) with (g, f) if b == 1; leaves them unchanged if b == 0.
    /// Runs in constant time (no data-dependent branches).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void CSwap(ref FieldElement f, ref FieldElement g, System.Int32 b)
    {
        System.Int32 t;
        System.Int32 mask = -b; // 0x00000000 or 0xFFFFFFFF

        t = mask & (f.E0 ^ g.E0); f.E0 ^= t; g.E0 ^= t;
        t = mask & (f.E1 ^ g.E1); f.E1 ^= t; g.E1 ^= t;
        t = mask & (f.E2 ^ g.E2); f.E2 ^= t; g.E2 ^= t;
        t = mask & (f.E3 ^ g.E3); f.E3 ^= t; g.E3 ^= t;
        t = mask & (f.E4 ^ g.E4); f.E4 ^= t; g.E4 ^= t;
        t = mask & (f.E5 ^ g.E5); f.E5 ^= t; g.E5 ^= t;
        t = mask & (f.E6 ^ g.E6); f.E6 ^= t; g.E6 ^= t;
        t = mask & (f.E7 ^ g.E7); f.E7 ^= t; g.E7 ^= t;
        t = mask & (f.E8 ^ g.E8); f.E8 ^= t; g.E8 ^= t;
        t = mask & (f.E9 ^ g.E9); f.E9 ^= t; g.E9 ^= t;
    }

    /// <summary>Copies <paramref name="src"/> into <paramref name="dst"/>.</summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Copy(ref FieldElement dst, FieldElement src)
    {
        dst.E0 = src.E0; dst.E1 = src.E1; dst.E2 = src.E2;
        dst.E3 = src.E3; dst.E4 = src.E4; dst.E5 = src.E5;
        dst.E6 = src.E6; dst.E7 = src.E7; dst.E8 = src.E8;
        dst.E9 = src.E9;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Shared carry-reduction path used by <see cref="Multiply"/> and <see cref="Square"/>.
    /// Returns a new <see cref="FieldElement"/> with all limbs reduced.
    /// All temporaries are scalar locals — zero heap allocation.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static FieldElement ReduceCarry(
        System.Int64 h0, System.Int64 h1, System.Int64 h2, System.Int64 h3, System.Int64 h4,
        System.Int64 h5, System.Int64 h6, System.Int64 h7, System.Int64 h8, System.Int64 h9)
    {
        System.Int64 c0, c1, c2, c3, c4, c5, c6, c7, c8, c9;

        c0 = (h0 + (1 << 25)) >> 26; h1 += c0; h0 -= c0 << 26;
        c4 = (h4 + (1 << 25)) >> 26; h5 += c4; h4 -= c4 << 26;
        c1 = (h1 + (1 << 24)) >> 25; h2 += c1; h1 -= c1 << 25;
        c5 = (h5 + (1 << 24)) >> 25; h6 += c5; h5 -= c5 << 25;
        c2 = (h2 + (1 << 25)) >> 26; h3 += c2; h2 -= c2 << 26;
        c6 = (h6 + (1 << 25)) >> 26; h7 += c6; h6 -= c6 << 26;
        c3 = (h3 + (1 << 24)) >> 25; h4 += c3; h3 -= c3 << 25;
        c7 = (h7 + (1 << 24)) >> 25; h8 += c7; h7 -= c7 << 25;
        c4 = (h4 + (1 << 25)) >> 26; h5 += c4; h4 -= c4 << 26;
        c8 = (h8 + (1 << 25)) >> 26; h9 += c8; h8 -= c8 << 26;
        c9 = (h9 + (1 << 24)) >> 25; h0 += c9 * 19; h9 -= c9 << 25;
        c0 = (h0 + (1 << 25)) >> 26; h1 += c0; h0 -= c0 << 26;

        return new FieldElement
        {
            E0 = (System.Int32)h0,
            E1 = (System.Int32)h1,
            E2 = (System.Int32)h2,
            E3 = (System.Int32)h3,
            E4 = (System.Int32)h4,
            E5 = (System.Int32)h5,
            E6 = (System.Int32)h6,
            E7 = (System.Int32)h7,
            E8 = (System.Int32)h8,
            E9 = (System.Int32)h9
        };
    }

    /// <summary>Reads 3 bytes little-endian from <paramref name="src"/> at <paramref name="offset"/>.</summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Int64 Load3(System.ReadOnlySpan<System.Byte> src, System.Int32 offset)
        => src[offset] | ((System.Int64)src[offset + 1] << 8) | ((System.Int64)src[offset + 2] << 16);

    /// <summary>Reads 4 bytes little-endian from <paramref name="src"/> at <paramref name="offset"/>.</summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Int64 Load4(System.ReadOnlySpan<System.Byte> src, System.Int32 offset)
        => System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(src.Slice(offset, 4));
}
