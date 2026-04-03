// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Framework.Security.Primitives;

/// <summary>
/// High-performance bitwise utilities for cryptographic operations.
/// These helpers keep the crypto code readable while still mapping to the
/// low-level arithmetic and comparisons required by the algorithms.
/// </summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
internal static class BitwiseOperations
{
    /// <summary>
    /// Unchecked integer exclusive or (XOR) operation.
    /// This is used heavily by stream ciphers when mixing keystream and payload.
    /// </summary>
    /// <param name="v">First operand.</param>
    /// <param name="w">Second operand.</param>
    /// <returns>The result of (v XOR w).</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static uint XOr(uint v, uint w) => v ^ w;

    /// <summary>
    /// Unchecked integer addition.
    /// ChaCha20 uses 32-bit unsigned addition modulo 2^32 for its quarter-round
    /// arithmetic, so this helper makes that intent explicit.
    /// </summary>
    /// <remarks>
    /// See <a href="https://tools.ietf.org/html/rfc7539#page-4">CHACHA20 Spec Section 2.1</a>.
    /// </remarks>
    /// <param name="v">First operand.</param>
    /// <param name="w">Second operand.</param>
    /// <returns>The result of (v + w) modulo 2^32.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static uint Add(uint v, uint w) => unchecked(v + w);

    /// <summary>
    /// Adds one using unchecked integer addition.
    /// This is a small convenience wrapper for the counter increments used by
    /// stream-cipher block scheduling.
    /// </summary>
    /// <remarks>
    /// See <a href="https://tools.ietf.org/html/rfc7539#page-4">CHACHA20 Spec Section 2.1</a>.
    /// </remarks>
    /// <param name="v">The value to increment.</param>
    /// <returns>The result of (v + 1) modulo 2^32.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static uint AddOne(uint v) => unchecked(v + 1);

    /// <summary>
    /// Unchecked integer subtraction.
    /// The subtraction is performed modulo 2^32 so it matches the arithmetic
    /// used by the cipher primitives.
    /// </summary>
    /// <param name="v">First operand.</param>
    /// <param name="w">Second operand.</param>
    /// <returns>The result of (v - w) modulo 2^32.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static uint Subtract(uint v, uint w) => unchecked(v - w);

    /// <summary>
    /// Compares two byte spans in a fixed-time manner to prevent timing attacks.
    /// The loop intentionally avoids early exit so an attacker cannot infer the
    /// first mismatching byte from timing differences.
    /// </summary>
    /// <param name="left">The first byte span to compare.</param>
    /// <param name="right">The second byte span to compare.</param>
    /// <returns>True if the byte spans are equal; otherwise, false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static bool FixedTimeEquals(System.ReadOnlySpan<byte> left, System.ReadOnlySpan<byte> right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        int result = 0;

        // Accumulate differences without branching so the timing stays independent of the first mismatch.
        for (int i = 0; i < left.Length; i++)
        {
            result |= left[i] ^ right[i];
        }

        return result == 0;
    }
}
