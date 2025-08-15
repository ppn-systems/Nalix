// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Framework.Randomization;

/// <summary>
/// A high-performance class for generating random numbers with various data types and ranges.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="SeededRandom"/> class with a specified seed.
/// </remarks>
/// <param name="seed">The seed value to initialize the random TransportProtocol generator.</param>
[System.Diagnostics.DebuggerStepThrough]
[System.Diagnostics.DebuggerDisplay("SeededRandom(Seed={_seed})")]
public sealed class SeededRandom(System.UInt32 seed) : MwcRandom(seed)
{
    /// <summary>
    /// Returns a random integer in the range [0, max).
    /// </summary>
    /// <param name="max">The exclusive upper bound.</param>
    /// <returns>A random integer in the range [0, max).</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Int32 Get(System.Int32 max)
    {
        if (max <= 0)
        {
            return 0;
        }

        // Avoid modulo bias for integer ranges
        System.UInt32 threshold =
            System.Int32.MaxValue -
            (System.UInt32)(System.Int32.MaxValue % max);

        System.UInt32 result;
        do
        {
            result = Get() & 0x7FFFFFFF; // Ensure positive value
        } while (result >= threshold);

        return (System.Int32)(result % (System.UInt32)max);
    }

    /// <summary>
    /// Returns a random unsigned integer in the range [0, max).
    /// </summary>
    /// <param name="max">The exclusive upper bound.</param>
    /// <returns>A random unsigned integer in the range [0, max).</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public new System.UInt32 Get(System.UInt32 max)
    {
        if (max == 0)
        {
            return 0;
        }

        // Fast path for power of 2
        if ((max & (max - 1)) == 0)
        {
            return Get() & (max - 1);
        }

        // Avoid modulo bias by rejecting values in the unfair region
        System.UInt32 threshold = RandMax - (RandMax % max);
        System.UInt32 result;
        do
        {
            result = Get();
        } while (result >= threshold);

        return result % max;
    }

    /// <summary>
    /// Returns a random unsigned long integer in the range [0, max).
    /// </summary>
    /// <param name="max">The exclusive upper bound.</param>
    /// <returns>A random unsigned long integer in the range [0, max).</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.UInt64 Get(System.UInt64 max)
    {
        if (max == 0)
        {
            return 0;
        }

        // Fast path for small values that fit in uint
        if (max <= System.UInt32.MaxValue)
        {
            return Get((System.UInt32)max);
        }

        // Optimize for powers of 2
        if ((max & (max - 1)) == 0)
        {
            return Get64() & (max - 1);
        }

        // Use rejection sampling to avoid modulo bias
        System.UInt64 threshold = System.UInt64.MaxValue - (System.UInt64.MaxValue % max);
        System.UInt64 result;
        do
        {
            result = Get64();
        } while (result >= threshold);

        return result % max;
    }

    /// <summary>
    /// Returns a random signed long integer in the range [0, max).
    /// </summary>
    /// <param name="max">The exclusive upper bound.</param>
    /// <returns>A random signed long integer in the range [0, max).</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Int64 Get(System.Int64 max)
    {
        if (max <= 0)
        {
            return 0;
        }

        // Ensure positive result
        System.UInt64 result = Get((System.UInt64)max);
        return (System.Int64)result;
    }

    /// <summary>
    /// Returns a random integer in the range [min, max).
    /// </summary>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The exclusive upper bound.</param>
    /// <returns>A random integer in the range [min, max).</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Int32 Get(System.Int32 min, System.Int32 max)
    {
        if (min >= max)
        {
            return min;
        }

        // Handle potential overflow when calculating range
        System.UInt32 range = (System.UInt32)(max - min);
        return min + (System.Int32)Get(range);
    }

    /// <summary>
    /// Returns a random unsigned integer in the range [min, max).
    /// </summary>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The exclusive upper bound.</param>
    /// <returns>A random unsigned integer in the range [min, max).</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public new System.UInt32 Get(System.UInt32 min, System.UInt32 max)
    {
        if (min >= max)
        {
            return min;
        }

        System.UInt32 range = max - min;
        return min + Get(range);
    }

    /// <summary>
    /// Returns a random unsigned long integer in the range [min, max).
    /// </summary>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The exclusive upper bound.</param>
    /// <returns>A random unsigned long integer in the range [min, max).</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.UInt64 Get(System.UInt64 min, System.UInt64 max)
    {
        if (min >= max)
        {
            return min;
        }

        System.UInt64 range = max - min;
        return min + Get(range);
    }

    /// <summary>
    /// Returns a random signed long integer in the range [min, max).
    /// Fixed signature to use long for both parameters.
    /// </summary>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The exclusive upper bound.</param>
    /// <returns>A random signed long integer in the range [min, max).</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Int64 Get(System.Int64 min, System.Int64 max)
    {
        if (min >= max)
        {
            return min;
        }

        // Handle negative range carefully
        System.UInt64 range = (System.UInt64)(max - min);
        return min + (System.Int64)Get(range);
    }

    /// <summary>
    /// Returns a random floating-point TransportProtocol in the range [0, max).
    /// </summary>
    /// <param name="max">The exclusive upper bound.</param>
    /// <returns>A random floating-point TransportProtocol in the range [0, max).</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Single Get(System.Single max) => max <= 0 ? 0 : GetFloat() * max;

    /// <summary>
    /// Returns a random double-precision floating-point TransportProtocol in the range [0, max).
    /// </summary>
    /// <param name="max">The exclusive upper bound.</param>
    /// <returns>A random double-precision floating-point TransportProtocol in the range [0, max).</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Double Get(System.Double max) => max <= 0 ? 0 : GetDouble() * max;

    /// <summary>
    /// Returns a random floating-point TransportProtocol in the range [min, max).
    /// </summary>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The exclusive upper bound.</param>
    /// <returns>A random floating-point TransportProtocol in the range [min, max).</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Single Get(System.Single min, System.Single max)
        => min >= max ? min : min + (GetFloat() * (max - min));

    /// <summary>
    /// Returns a random double-precision floating-point TransportProtocol in the range [min, max).
    /// </summary>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The exclusive upper bound.</param>
    /// <returns>A random double-precision floating-point TransportProtocol in the range [min, max).</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Double Get(System.Double min, System.Double max)
        => min >= max ? min : min + (GetDouble() * (max - min));

    /// <summary>
    /// Returns a random boolean with the specified probability of being true.
    /// </summary>
    /// <param name="probability">The probability of returning true (0.0 to 1.0).</param>
    /// <returns>A random boolean.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Boolean GetBool(System.Double probability = 0.5)
        => probability > 0.0 && (probability >= 1.0 || GetDouble() < probability);

    /// <summary>
    /// Returns a random floating-point TransportProtocol in the range [0.0, 1.0).
    /// </summary>
    /// <remarks>
    /// This implementation ensures uniform distribution across the entire range
    /// and avoids common precision issues in floating-point random generation.
    /// </remarks>
    /// <returns>A random float in the range [0.0, 1.0).</returns>
    // Use 24 bits (mantissa size for float) to ensure uniform distribution
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public new System.Single GetFloat() => (Get() >> 8) * (1.0f / 16777216.0f);

    /// <summary>
    /// Returns a random double-precision floating-point TransportProtocol in the range [0.0, 1.0).
    /// </summary>
    /// <remarks>
    /// This implementation ensures uniform distribution across the entire range
    /// and uses the full 53-bit precision available in a double.
    /// </remarks>
    /// <returns>A random double in the range [0.0, 1.0).</returns>
    // Use all 53 bits of precision available in a double
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public new System.Double GetDouble() => (Get64() >> 11) * (1.0 / 9007199254740992.0);

    /// <summary>
    /// Fills the specified buffer with random bytes.
    /// </summary>
    /// <param name="buffer">The buffer to fill with random bytes.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public new void NextBytes(System.Span<System.Byte> buffer) => base.NextBytes(buffer);
}