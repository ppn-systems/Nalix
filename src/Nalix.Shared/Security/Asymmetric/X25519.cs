// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Framework.Random;

namespace Nalix.Shared.Security.Asymmetric;

/// <summary>
/// Provides methods for generating and using X25519 key pairs for elliptic curve Diffie–Hellman
/// based on Curve25519 (RFC 7748).
/// </summary>
public static class X25519
{
    /// <summary>
    /// Represents an X25519 key pair consisting of a private key and a public key.
    /// </summary>
    [System.Runtime.CompilerServices.SkipLocalsInit]
    public struct X25519KeyPair
    {
        /// <summary>The private key (32 bytes).</summary>
        public System.Byte[] PrivateKey;

        /// <summary>The public key (32 bytes).</summary>
        public System.Byte[] PublicKey;
    }

    /// <summary>
    /// Generates a new X25519 key pair with a cryptographically random private key.
    /// </summary>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static X25519KeyPair GenerateKeyPair()
    {
        X25519KeyPair key = new() { PrivateKey = new System.Byte[32] };
        Csprng.Fill(key.PrivateKey);

        // Clamp per https://cr.yp.to/ecdh.html
        key.PrivateKey[0] &= 248;
        key.PrivateKey[31] &= 127;
        key.PrivateKey[31] |= 64;

        key.PublicKey = Curve25519.ScalarMultiplication(key.PrivateKey, Curve25519.Basepoint);
        return key;
    }

    /// <summary>
    /// Derives the public key from a provided 32-byte private key.
    /// </summary>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static X25519KeyPair GenerateKeyFromPrivateKey(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] privateKey)
    {
        X25519KeyPair key = new() { PrivateKey = privateKey };
        key.PublicKey = Curve25519.ScalarMultiplication(key.PrivateKey, Curve25519.Basepoint);
        return key;
    }

    /// <summary>
    /// Computes a shared secret via X25519 scalar multiplication
    /// (<paramref name="myPrivateKey"/> × <paramref name="otherPublicKey"/>).
    /// </summary>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Byte[] Agreement(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] myPrivateKey,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] otherPublicKey)
        => Curve25519.ScalarMultiplication(myPrivateKey, otherPublicKey);
}

[System.Diagnostics.StackTraceHidden]
internal static class Curve25519
{
    /// <summary>The Curve25519 base point u = 9.</summary>
    public static readonly System.Byte[] Basepoint =
    [
        9, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
    ];

    // ── Core Montgomery ladder ────────────────────────────────────────────────

    /// <summary>
    /// Performs the raw scalar multiplication on Curve25519.
    /// All <see cref="FieldElement"/> values live on the stack (struct, no heap).
    /// The only heap allocation is the 32-byte <paramref name="output"/> array
    /// written via <see cref="FieldElement.ToBytes"/>.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void ScalarMult(
        System.ReadOnlySpan<System.Byte> scalar,
        System.ReadOnlySpan<System.Byte> baseIn,
        System.Span<System.Byte> output)
    {
        // Clamp a copy of the scalar.
        System.Span<System.Byte> e = stackalloc System.Byte[32];
        scalar.CopyTo(e);
        e[0] &= 248;
        e[31] &= 127;
        e[31] |= 64;

        // All FieldElement locals live on the stack — struct semantics, zero heap.
        FieldElement x1 = new(baseIn);
        FieldElement x2 = default; x2.One();
        FieldElement z2 = default;          // zero-initialized by default
        FieldElement x3 = default; FieldElement.Copy(ref x3, x1);
        FieldElement z3 = default; z3.One();
        FieldElement tmp0, tmp1;

        System.Int32 swap = 0;

        for (System.Int32 pos = 254; pos >= 0; pos--)
        {
            System.Byte b = (System.Byte)(e[pos / 8] >> (pos & 7));
            b &= 1;
            swap ^= b;
            FieldElement.CSwap(ref x2, ref x3, swap);
            FieldElement.CSwap(ref z2, ref z3, swap);
            swap = b;

            tmp0 = x3 - z3;
            tmp1 = x2 - z2;
            x2 += z2;
            z2 = x3 + z3;
            z3 = tmp0.Multiply(x2);
            z2 = z2.Multiply(tmp1);
            tmp0 = tmp1.Square();
            tmp1 = x2.Square();
            x3 = z3 + z2;
            z2 = z3 - z2;
            x2 = tmp1.Multiply(tmp0);
            tmp1 -= tmp0;
            z2 = z2.Square();
            z3 = tmp1.Mul121666();
            x3 = x3.Square();
            tmp0 += z3;
            z3 = x1.Multiply(z2);
            z2 = tmp1.Multiply(tmp0);
        }

        FieldElement.CSwap(ref x2, ref x3, swap);
        FieldElement.CSwap(ref z2, ref z3, swap);

        z2 = z2.Invert();
        x2 = x2.Multiply(z2);

        // Write result directly into caller-supplied span — no extra byte[] alloc.
        x2.ToBytes(output);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// X25519 scalar multiplication (scalar × point) per RFC 7748 §5.
    /// Both inputs must be 32-byte arrays; the return value is a new 32-byte array.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Byte[] ScalarMultiplication(
        System.Byte[] scalar,
        System.Byte[] point)
    {
        if (scalar.Length != 32)
        {
            throw new System.ArgumentException("Length of scalar must be 32", nameof(scalar));
        }

        if (point.Length != 32)
        {
            throw new System.ArgumentException("Length of point must be 32", nameof(point));
        }

        // Exactly one heap allocation: the 32-byte result array.
        System.Byte[] result = System.GC.AllocateUninitializedArray<System.Byte>(32);
        ScalarMult(scalar, point, result);

        // Constant-time low-order-point check (equivalent to subtle.ConstantTimeCompare).
        System.Byte v = 0;
        for (System.Int32 i = 0; i < 32; i++)
        {
            v |= result[i];
        }

        // If all bytes are zero the input was a low-order point.
        return (System.Int32)(((System.UInt32)(v ^ 0) - 1) >> 31) == 1
            ? throw new System.InvalidOperationException("Bad input point: low order point")
            : result;
    }
}