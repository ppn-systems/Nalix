// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Codec.Security.Asymmetric;

[System.Diagnostics.StackTraceHidden]
[System.Runtime.CompilerServices.SkipLocalsInit]
internal static class Curve25519
{
    #region Constants

    /// <summary>
    /// The Curve25519 base point u = 9.
    /// </summary>
    public static readonly byte[] Basepoint =
    [
        9, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
    ];

    #endregion Constants

    #region APIs

    /// <summary>
    /// X25519 scalar multiplication (scalar × point) per RFC 7748 §5.
    /// Both inputs must be at least 32 bytes; the return value is a new 32-byte array.
    /// </summary>
    /// <param name="scalar">The 32-byte scalar.</param>
    /// <param name="point">The 32-byte point.</param>
    /// <exception cref="System.ArgumentException"></exception>
    /// <exception cref="System.InvalidOperationException"></exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static byte[] ScalarMultiplication(System.ReadOnlySpan<byte> scalar, System.ReadOnlySpan<byte> point)
    {
        if (scalar.Length < 32)
        {
            throw new System.ArgumentException("Length of scalar must be at least 32", nameof(scalar));
        }

        if (point.Length < 32)
        {
            throw new System.ArgumentException("Length of point must be at least 32", nameof(point));
        }

        // Exactly one heap allocation: the 32-byte result array.
        byte[] result = GC.AllocateUninitializedArray<byte>(32);
        ScalarMult(scalar[..32], point[..32], result);

        // Constant-time low-order-point check.
        byte v = 0;
        for (int i = 0; i < 32; i++)
        {
            v |= result[i];
        }

        // If all bytes are zero the input was a low-order point.
        return (int)(((uint)(v ^ 0) - 1) >> 31) == 1
            ? throw new System.InvalidOperationException("Bad input point: low order point")
            : result;
    }

    #endregion APIs

    #region Core Montgomery ladder

    /// <summary>
    /// Performs the raw scalar multiplication on Curve25519.
    /// All <see cref="FieldElement"/> values live on the stack (struct, no heap).
    /// The only heap allocation is the 32-byte <paramref name="output"/> array
    /// written via <see cref="FieldElement.ToBytes"/>.
    /// </summary>
    /// <param name="scalar"></param>
    /// <param name="baseIn"></param>
    /// <param name="output"></param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void ScalarMult(ReadOnlySpan<byte> scalar, ReadOnlySpan<byte> baseIn, Span<byte> output)
    {
        // 1. Clamp a copy of the scalar (keep stackalloc to zero-heap)
        Span<byte> e = stackalloc byte[32];
        scalar.CopyTo(e);
        e[0] &= 248;
        e[31] &= 127;
        e[31] |= 64;

        // 2. Initialize FieldElements on the stack
        FieldElement x1 = new(baseIn);
        FieldElement x2 = default; x2.One();
        FieldElement z2 = default;
        FieldElement x3 = default; FieldElement.Copy(ref x3, in x1);
        FieldElement z3 = default; z3.One();

        FieldElement tmp0, tmp1;

        int swap = 0;

        // 3. Main computation loop (255 iterations)
        for (int pos = 254; pos >= 0; pos--)
        {
            byte b = (byte)(e[pos / 8] >> (pos & 7));
            b &= 1;
            swap ^= b;
            FieldElement.CSwap(ref x2, ref x3, swap);
            FieldElement.CSwap(ref z2, ref z3, swap);
            swap = b;

            tmp0 = x3 - z3;
            tmp1 = x2 - z2;
            x2 += z2;
            z2 = x3 + z3;

            tmp0.Multiply(in x2, out z3);   // z3 = tmp0 * x2
            z2.Multiply(in tmp1, out z2); // z2 = z2 * tmp1

            tmp1.Square(out tmp0);        // tmp0 = tmp1^2
            x2.Square(out tmp1);          // tmp1 = x2^2

            x3 = z3 + z2;
            z2 = z3 - z2;

            tmp1.Multiply(in tmp0, out x2); // x2 = tmp1 * tmp0
            tmp1 -= tmp0;

            z2.Square(out z2);              // z2 = z2^2
            tmp1.Mul121666(out z3);         // z3 = tmp1 * 121666
            x3.Square(out x3);              // x3 = x3^2

            tmp0 += z3;
            x1.Multiply(in z2, out z3);     // z3 = x1 * z2
            tmp1.Multiply(in tmp0, out z2); // z2 = tmp1 * tmp0
        }

        FieldElement.CSwap(ref x2, ref x3, swap);
        FieldElement.CSwap(ref z2, ref z3, swap);

        // 4. Invert and multiply the final result (using out)
        z2.Invert(out z2);               // z2 = z2^-1
        x2.Multiply(in z2, out x2);      // x2 = x2 * z2

        // 5. Serialize directly to output
        x2.ToBytes(output);
    }

    #endregion Core Montgomery ladder
}

