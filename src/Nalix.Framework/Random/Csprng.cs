// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Nalix.Framework.Injection;
using Nalix.Framework.Random.Core;

namespace Nalix.Framework.Random;

/// <summary>
/// High-performance cryptographically strong random number generator
/// based on the Xoshiro256++ algorithm with additional entropy sources.
/// </summary>
[StackTraceHidden]
[DebuggerNonUserCode]
[DebuggerStepThrough]
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public static class Csprng
{
    #region Fields

    private static string DebuggerDisplay => "Csprng(primary=OS)";

    private static readonly Action<Span<byte>> s_f;

    #endregion Fields

    #region Constructor

    static Csprng()
    {
        Action<Span<byte>> f = OsCsprng.Fill;
        Span<byte> probe = stackalloc byte[16];

        try
        {
            f(probe);
            s_f = f;
        }
        catch (InvalidOperationException)
        {
            OsRandom.Attach();
            s_f = OsRandom.Fill;
        }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Info($"[FW.Csprng] init using {(s_f == OsCsprng.Fill ? "OS_CSPRNG" : "FA_RANDOM")}");
    }

    #endregion Constructor

    #region APIs

    /// <summary>
    /// Fills the provided span with cryptographically strong random bytes.
    /// </summary>
    /// <param name="data">The span to fill with random bytes.</param>
    /// <remarks>
    /// Thread-safe. Uses OS-level CSPRNG (e.g., BCryptGenRandom on Windows, getrandom on Linux).
    /// Falls back to high-quality pseudo-random generator if OS CSPRNG is unavailable.
    /// Suitable for cryptographic purposes including key generation, nonces, and IVs.
    /// </remarks>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Fill(Span<byte> data)
    {
        if (data.Length == 0)
        {
            return;
        }

        s_f(data);
    }

    /// <summary>
    /// Generates a secure 12-byte nonce (96 bits) suitable for most authenticated encryption schemes.
    /// </summary>
    /// <param name="length">The length of the nonce in bytes. Default is 12 bytes (96 bits).</param>
    /// <returns>A cryptographically secure nonce.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when length is less than or equal to zero.</exception>
    /// <remarks>
    /// 96-bit (12-byte) nonces are recommended for most AEAD schemes like AES-GCM and ChaCha20-Poly1305.
    /// Never reuse a nonce with the same key in authenticated encryption.
    /// </remarks>
    public static byte[] CreateNonce(int length = 12)
    {
        if (length <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(length), length, "Nonce length must be a positive integer.");
        }

        byte[] nonce = new byte[length];
        s_f(nonce);
        return nonce;
    }

    #region Get

    /// <summary>
    /// Generates a random byte array of the specified length.
    /// </summary>
    /// <param name="length">The number of random bytes to generate.</param>
    /// <returns>A byte array filled with cryptographically secure random data.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when length is negative.</exception>
    /// <remarks>
    /// Thread-safe. Returns an empty array if length is 0.
    /// Use this for generating cryptographic keys, tokens, and other security-sensitive data.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static byte[] GetBytes(int length)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), length, "Length cannot be negative.");
        }

        if (length == 0)
        {
            return [];
        }

        byte[] bytes = new byte[length];
        s_f(bytes);
        return bytes;
    }

    /// <summary>
    /// Gets a random integer in the range [min, max).
    /// </summary>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The exclusive upper bound.</param>
    /// <returns>A cryptographically secure random integer in the specified range.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when min is greater than or equal to max.</exception>
    /// <remarks>
    /// Uses rejection sampling to ensure unbiased distribution across the entire range.
    /// Thread-safe. Suitable for security-sensitive applications requiring unpredictable integers.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static int GetInt32(
        int min,
        int max)
    {
        if (min >= max)
        {
            throw new ArgumentOutOfRangeException(nameof(max), max, "Max must be greater than min.");
        }

        ulong range = (ulong)((long)max - min);

        // Compute mask on 64-bit to avoid overflow/bias
        int bits = 64 - System.Numerics.BitOperations.LeadingZeroCount(range - 1);
        ulong mask = bits >= 64 ? ulong.MaxValue : ((1UL << bits) - 1);

        ulong r;
        do
        {
            r = NextUInt64() & mask;
        } while (r >= range);

        return (int)(r + (ulong)min);
    }

    /// <summary>
    /// Gets a random integer in the range [0, max).
    /// </summary>
    /// <param name="max">The exclusive upper bound.</param>
    /// <returns>A cryptographically secure random integer in the specified range.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when max is less than or equal to 0.</exception>
    /// <remarks>
    /// Thread-safe. Uses unbiased rejection sampling for uniform distribution.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static int GetInt32(
        int max) => GetInt32(0, max);

    #endregion Get

    #region Next

    /// <summary>
    /// Fills the given byte array with cryptographically strong random values.
    /// </summary>
    /// <param name="buffer">The buffer to fill with random bytes.</param>
    /// <exception cref="ArgumentNullException">Thrown when buffer is null.</exception>
    /// <remarks>
    /// Thread-safe. Equivalent to Fill(buffer.AsSpan()).
    /// </remarks>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void NextBytes(
        byte[] buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        s_f(buffer);
    }

    /// <summary>
    /// Fills the given span with cryptographically strong random values.
    /// </summary>
    /// <param name="buffer">The span to fill with random bytes.</param>
    /// <remarks>
    /// Thread-safe. Preferred over the array overload for performance-critical code.
    /// </remarks>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void NextBytes(
        Span<byte> buffer) => Fill(buffer);

    /// <summary>
    /// Generates a cryptographically strong 32-bit random integer.
    /// </summary>
    /// <returns>A random 32-bit unsigned integer.</returns>
    /// <remarks>
    /// Thread-safe. Suitable for generating unpredictable identifiers and tokens.
    /// </remarks>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static uint NextUInt32()
    {
        Span<byte> b = stackalloc byte[4];
        s_f(b);
        return System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(b);
    }

    /// <summary>
    /// Generates a cryptographically strong 64-bit random integer.
    /// </summary>
    /// <returns>A random 64-bit unsigned integer.</returns>
    /// <remarks>
    /// Thread-safe. Useful for generating high-entropy identifiers and session tokens.
    /// </remarks>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ulong NextUInt64()
    {
        Span<byte> b = stackalloc byte[8];
        s_f(b);
        return System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(b);
    }

    /// <summary>
    /// Generates a random double in the range [0.0, 1.0).
    /// </summary>
    /// <returns>A random double with uniform distribution.</returns>
    /// <remarks>
    /// Thread-safe. Uses 53 bits of precision (full mantissa of double).
    /// Suitable for Monte Carlo simulations and statistical sampling.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static double NextDouble() => (NextUInt64() >> 11) * (1.0 / 9007199254740992.0);

    #endregion Next

    #endregion APIs
}
