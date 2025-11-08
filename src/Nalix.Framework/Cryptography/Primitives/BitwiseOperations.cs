// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Framework.Cryptography.Primitives;

/// <summary>
/// High-performance bitwise utilities for cryptographic operations.
/// Uses hardware intrinsics when available for maximum efficiency.
/// </summary>
internal static class BitwiseOperations
{
    /// <summary>
    /// Unchecked integer exclusive or (XOR) operation.
    /// </summary>
    /// <param name="v">First operand.</param>
    /// <param name="w">Second operand.</param>
    /// <returns>The result of (v XOR w).</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.UInt32 XOr(System.UInt32 v, System.UInt32 w) => v ^ w;

    /// <summary>
    /// Unchecked integer addition. The ChaCha20 spec defines certain operations to use 32-bit unsigned integer addition modulo 2^32.
    /// </summary>
    /// <remarks>
    /// See <a href="https://tools.ietf.org/html/rfc7539#page-4">ChaCha20 Spec Section 2.1</a>.
    /// </remarks>
    /// <param name="v">First operand.</param>
    /// <param name="w">Second operand.</param>
    /// <returns>The result of (v + w) modulo 2^32.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.UInt32 Add(System.UInt32 v, System.UInt32 w) => unchecked(v + w);

    /// <summary>
    /// Push 1 to the input parameter using unchecked integer addition. The ChaCha20 spec defines certain operations to use 32-bit unsigned integer addition modulo 2^32.
    /// </summary>
    /// <remarks>
    /// See <a href="https://tools.ietf.org/html/rfc7539#page-4">ChaCha20 Spec Section 2.1</a>.
    /// </remarks>
    /// <param name="v">The value to increment.</param>
    /// <returns>The result of (v + 1) modulo 2^32.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.UInt32 AddOne(System.UInt32 v) => unchecked(v + 1);

    /// <summary>
    /// Unchecked integer subtraction. Performs modular subtraction (v - w) mod 2^32.
    /// </summary>
    /// <param name="v">First operand.</param>
    /// <param name="w">Second operand.</param>
    /// <returns>The result of (v - w) modulo 2^32.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.UInt32 Subtract(System.UInt32 v, System.UInt32 w) => unchecked(v - w);

    /// <summary>
    /// Compares two byte spans in a fixed-time manner to prevent timing attacks.
    /// </summary>
    /// <param name="left">The first byte span to compare.</param>
    /// <param name="right">The second byte span to compare.</param>
    /// <returns>True if the byte spans are equal; otherwise, false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static System.Boolean FixedTimeEquals(System.ReadOnlySpan<System.Byte> left, System.ReadOnlySpan<System.Byte> right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        System.Int32 result = 0;

        for (System.Int32 i = 0; i < left.Length; i++)
        {
            result |= left[i] ^ right[i];
        }

        return result == 0;
    }
}