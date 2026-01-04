// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging;
using Nalix.Framework.Injection;
using Nalix.Framework.Random.Core;

namespace Nalix.Framework.Random;

/// <summary>
/// High-performance cryptographically strong random ProtocolType generator
/// based on the Xoshiro256++ algorithm with additional entropy sources.
/// </summary>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerNonUserCode]
[System.Diagnostics.DebuggerStepThrough]
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
public static class Csprng
{
    private static System.String DebuggerDisplay => "Csprng(primary=OS)";

    private static readonly System.Action<System.Span<System.Byte>> _f;

    static Csprng()
    {
        System.Action<System.Span<System.Byte>> f = OsCsprng.Fill;
        System.Span<System.Byte> probe = stackalloc System.Byte[16];

        try
        {
            f(probe);
            _f = f;
        }
        catch
        {
            OsRandom.Attach();
            _f = OsRandom.Fill;
        }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Info($"[FW.Csprng] init using {(_f == OsCsprng.Fill ? "OS_CSPRNG" : "FA_RANDOM")}");
    }

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
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static void Fill([System.Diagnostics.CodeAnalysis.NotNull] System.Span<System.Byte> data)
    {
        if (data.Length == 0)
        {
            return;
        }

        _f(data);
    }

    /// <summary>
    /// Generates a secure 12-byte nonce (96 bits) suitable for most authenticated encryption schemes.
    /// </summary>
    /// <param name="length">The length of the nonce in bytes. Default is 12 bytes (96 bits).</param>
    /// <returns>A cryptographically secure nonce.</returns>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown when length is less than or equal to zero.</exception>
    /// <remarks>
    /// 96-bit (12-byte) nonces are recommended for most AEAD schemes like AES-GCM and ChaCha20-Poly1305.
    /// Never reuse a nonce with the same key in authenticated encryption.
    /// </remarks>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Byte[] CreateNonce([System.Diagnostics.CodeAnalysis.NotNull] System.Int32 length = 12)
    {
        if (length <= 0)
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(length), length, "Nonce length must be a positive integer.");
        }

        System.Byte[] nonce = new System.Byte[length];
        _f(nonce);
        return nonce;
    }

    /// <summary>
    /// Generates a random byte array of the specified length.
    /// </summary>
    /// <param name="length">The number of random bytes to generate.</param>
    /// <returns>A byte array filled with cryptographically secure random data.</returns>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown when length is negative.</exception>
    /// <remarks>
    /// Thread-safe. Returns an empty array if length is 0.
    /// Use this for generating cryptographic keys, tokens, and other security-sensitive data.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Byte[] GetBytes([System.Diagnostics.CodeAnalysis.NotNull] System.Int32 length)
    {
        if (length < 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(length), length, "Length cannot be negative.");
        }

        if (length == 0)
        {
            return [];
        }

        System.Byte[] bytes = new System.Byte[length];
        _f(bytes);
        return bytes;
    }

    /// <summary>
    /// Gets a random integer in the range [min, max).
    /// </summary>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The exclusive upper bound.</param>
    /// <returns>A cryptographically secure random integer in the specified range.</returns>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown when min is greater than or equal to max.</exception>
    /// <remarks>
    /// Uses rejection sampling to ensure unbiased distribution across the entire range.
    /// Thread-safe. Suitable for security-sensitive applications requiring unpredictable integers.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Int32 GetInt32(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Int32 min,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Int32 max)
    {
        if (min >= max)
        {
            throw new System.ArgumentOutOfRangeException(nameof(max), max, "Max must be greater than min.");
        }

        System.UInt64 range = (System.UInt64)((System.Int64)max - min);

        // Compute mask on 64-bit to avoid overflow/bias
        System.Int32 bits = 64 - System.Numerics.BitOperations.LeadingZeroCount(range - 1);
        System.UInt64 mask = bits >= 64 ? System.UInt64.MaxValue : ((1UL << bits) - 1);

        System.UInt64 r;
        do
        {
            r = NextUInt64() & mask;
        } while (r >= range);

        return (System.Int32)(r + (System.UInt64)min);
    }

    /// <summary>
    /// Gets a random integer in the range [0, max).
    /// </summary>
    /// <param name="max">The exclusive upper bound.</param>
    /// <returns>A cryptographically secure random integer in the specified range.</returns>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown when max is less than or equal to 0.</exception>
    /// <remarks>
    /// Thread-safe. Uses unbiased rejection sampling for uniform distribution.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Int32 GetInt32(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Int32 max) => GetInt32(0, max);

    /// <summary>
    /// Fills the given byte array with cryptographically strong random values.
    /// </summary>
    /// <param name="buffer">The buffer to fill with random bytes.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when buffer is null.</exception>
    /// <remarks>
    /// Thread-safe. Equivalent to Fill(buffer.AsSpan()).
    /// </remarks>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static void NextBytes(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] buffer)
    {
        System.ArgumentNullException.ThrowIfNull(buffer);
        _f(buffer);
    }

    /// <summary>
    /// Fills the given span with cryptographically strong random values.
    /// </summary>
    /// <param name="buffer">The span to fill with random bytes.</param>
    /// <remarks>
    /// Thread-safe. Preferred over the array overload for performance-critical code.
    /// </remarks>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static void NextBytes(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<System.Byte> buffer) => Fill(buffer);

    /// <summary>
    /// Generates a cryptographically strong 32-bit random integer.
    /// </summary>
    /// <returns>A random 32-bit unsigned integer.</returns>
    /// <remarks>
    /// Thread-safe. Suitable for generating unpredictable identifiers and tokens.
    /// </remarks>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.UInt32 NextUInt32()
    {
        System.Span<System.Byte> b = stackalloc System.Byte[4];
        _f(b);
        return System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(b);
    }

    /// <summary>
    /// Generates a cryptographically strong 64-bit random integer.
    /// </summary>
    /// <returns>A random 64-bit unsigned integer.</returns>
    /// <remarks>
    /// Thread-safe. Useful for generating high-entropy identifiers and session tokens.
    /// </remarks>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.UInt64 NextUInt64()
    {
        System.Span<System.Byte> b = stackalloc System.Byte[8];
        _f(b);
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Double NextDouble() => (NextUInt64() >> 11) * (1.0 / 9007199254740992.0);

    #endregion APIs
}