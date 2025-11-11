// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Shared.Security.Primitives;

/// <summary>
/// Provides extension methods for BigInteger operations with modular arithmetic.
/// </summary>
internal static class BigInteger
{
    /// <summary>
    /// Adds two BigInteger values with modulo operation.
    /// </summary>
    /// <param name="a">First BigInteger value.</param>
    /// <param name="b">Second BigInteger value.</param>
    /// <param name="mod">Modulo value.</param>
    /// <returns>The result of (a + b) % mod.</returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Numerics.BigInteger ModAdd(
        this System.Numerics.BigInteger a,
        System.Numerics.BigInteger b, System.Numerics.BigInteger mod)
    {
        a += b;
        if (a >= mod)
        {
            a -= mod;
        }
        else if (a < 0)
        {
            a += mod;
        }

        return a;
    }

    /// <summary>
    /// Subtracts one BigInteger value from another with modulo operation.
    /// </summary>
    /// <param name="a">First BigInteger value.</param>
    /// <param name="b">Second BigInteger value.</param>
    /// <param name="mod">Modulo value.</param>
    /// <returns>The result of (a - b) % mod.</returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Numerics.BigInteger ModSub(
        this System.Numerics.BigInteger a,
        System.Numerics.BigInteger b, System.Numerics.BigInteger mod)
    {
        a -= b;
        if (a < 0)
        {
            a += mod;
        }
        else if (a >= mod)
        {
            a -= mod;
        }

        return a;
    }

    /// <summary>
    /// Multiplies two BigInteger values with modulo operation.
    /// </summary>
    /// <param name="a">First BigInteger value.</param>
    /// <param name="b">Second BigInteger value.</param>
    /// <param name="mod">Modulo value.</param>
    /// <returns>The result of (a * b) % mod.</returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Numerics.BigInteger MultiplyMod(
        this System.Numerics.BigInteger a,
        System.Numerics.BigInteger b, System.Numerics.BigInteger mod) => a * b % mod;

    /// <summary>
    /// Performs a modulo operation and ensures the result is non-negative.
    /// </summary>
    /// <param name="num">The BigInteger value.</param>
    /// <param name="modulo">The modulo value.</param>
    /// <returns>The result of num % modulo, adjusted to be non-negative.</returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Numerics.BigInteger Mod(
        this System.Numerics.BigInteger num, System.Numerics.BigInteger modulo)
    {
        System.Numerics.BigInteger result = num % modulo;
        return result < 0 ? result + modulo : result;
    }
}