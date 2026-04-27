// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Codec.Security.Primitives;

/// <summary>
/// High-performance bitwise utilities for cryptographic operations.
/// These helpers keep the crypto code readable while still mapping to the
/// low-level arithmetic and comparisons required by the algorithms.
/// </summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public static class BitwiseOperations
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
    internal static uint XOr(uint v, uint w) => v ^ w;

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
    internal static uint Add(uint v, uint w) => unchecked(v + w);

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
    internal static uint AddOne(uint v) => unchecked(v + 1);

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
    internal static uint Subtract(uint v, uint w) => unchecked(v - w);

    /// <summary>
    /// Compares two byte spans in a fixed-time manner to prevent timing attacks.
    /// The execution time depends only on the length of the spans, not their content.
    /// </summary>
    /// <param name="left">The first byte span to compare.</param>
    /// <param name="right">The second byte span to compare.</param>
    /// <returns>True if the byte spans are equal; otherwise, false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.NoOptimization)]
    public static bool FixedTimeEquals(System.ReadOnlySpan<byte> left, System.ReadOnlySpan<byte> right)
    {
        // Even if lengths differ, we want to minimize timing information.
        // However, if lengths are public (like tag lengths), a simple check is acceptable.
        // For maximum security, we compare the minimum length and accumulate bits.
        if (left.Length != right.Length)
        {
            return false;
        }

        int result = 0;
        for (int i = 0; i < left.Length; i++)
        {
            result |= left[i] ^ right[i];
        }

        return result == 0;
    }

    /// <summary>
    /// Checks if all bytes in the given ReadOnlySpan&lt;byte&gt; are zero in constant-time.
    /// This prevents timing attacks that could reveal information about sensitive buffers.
    /// </summary>
    /// <param name="value">The span of bytes to check.</param>
    /// <returns>True if all bytes are zero, otherwise false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.NoOptimization)]
    public static unsafe bool IsZero(ReadOnlySpan<byte> value)
    {
        if (value.IsEmpty)
        {
            return true;
        }

        int accumulator = 0;
        fixed (byte* ptr = value)
        {
            int len = value.Length;
            for (int i = 0; i < len; i++)
            {
                accumulator |= ptr[i];
            }
        }

        return accumulator == 0;
    }
}
