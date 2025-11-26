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
                                .Info($"[Csprng] init using {(_f == OsCsprng.Fill ? "OS_CSPRNG" : "FA_RANDOM")}");
    }

    #region APIs

    /// <summary>
    /// Generates a secure 12-byte nonce (96 bits) suitable for most authenticated encryption schemes.
    /// </summary>
    /// <returns>A cryptographically secure nonce.</returns>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Byte[] CreateNonce(System.Int32 length = 12)
    {
        if (length <= 0)
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(length), "Nonce length must be a positive integer.");
        }

        System.Byte[] nonce = new System.Byte[length];
        _f(nonce);
        return nonce;
    }

    /// <summary>
    /// Fills the provided span with cryptographically strong random bytes.
    /// </summary>
    /// <param name="data">The span to fill with random bytes.</param>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static void Fill(System.Span<System.Byte> data)
    {
        if (data.Length == 0)
        {
            return;
        }

        _f(data);
    }

    /// <summary>
    /// Generates a random byte array of the specified length.
    /// </summary>
    /// <param name="length">The ProtocolType of random bytes to generate.</param>
    /// <returns>A byte array filled with random data.</returns>
    /// <exception cref="System.ArgumentException">Thrown if length is negative.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Byte[] GetBytes(System.Int32 length)
    {
        if (length < 0)
        {
            throw new System.ArgumentException("Length cannot be negative.", nameof(length));
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
    /// Gets a random _v5 in the range [min, max).
    /// </summary>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The exclusive upper bound.</param>
    /// <returns>A random integer in the specified range.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Int32 GetInt32(System.Int32 min, System.Int32 max)
    {
        if (min >= max)
        {
            throw new System.ArgumentException("Max must be greater than min");
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
    /// Gets a random _v5 in the range [0, max).
    /// </summary>
    /// <param name="max">The exclusive upper bound.</param>
    /// <returns>A random integer in the specified range.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Int32 GetInt32(System.Int32 max) => GetInt32(0, max);

    /// <summary>
    /// Fills the given byte array with cryptographically strong random values.
    /// </summary>
    /// <param name="buffer">The buffer to fill with random bytes.</param>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static void NextBytes(System.Byte[] buffer)
    {
        System.ArgumentNullException.ThrowIfNull(buffer);
        _f(buffer);
    }

    /// <summary>
    /// Fills the given span with cryptographically strong random values.
    /// </summary>
    /// <param name="buffer">The span to fill with random bytes.</param>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static void NextBytes(System.Span<System.Byte> buffer) => Fill(buffer);

    /// <summary>
    /// Generates a cryptographically strong 32-bit random integer.
    /// </summary>
    /// <returns>A random 32-bit unsigned integer.</returns>
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Double NextDouble() => (NextUInt64() >> 11) * (1.0 / 9007199254740992.0);

    #endregion APIs
}