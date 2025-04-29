using System;
using System.Runtime.CompilerServices;

namespace Nalix.Randomization;

/// <summary>
/// A high-performance class for generating random numbers with various data types and ranges.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="Rand"/> class with a specified seed.
/// </remarks>
/// <param name="seed">The seed value to initialize the random Number generator.</param>
public sealed class Rand(uint seed) : MwcRandom(seed)
{
    /// <summary>
    /// Returns a random integer in the range [0, max).
    /// </summary>
    /// <param name="max">The exclusive upper bound.</param>
    /// <returns>A random integer in the range [0, max).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Get(int max)
    {
        if (max <= 0)
            return 0;

        // Avoid modulo bias for integer ranges
        uint threshold = ((uint)(int.MaxValue) - (uint)(int.MaxValue % max));
        uint result;
        do
        {
            result = Get() & 0x7FFFFFFF; // Ensure positive value
        } while (result >= threshold);

        return (int)(result % (uint)max);
    }

    /// <summary>
    /// Returns a random unsigned integer in the range [0, max).
    /// </summary>
    /// <param name="max">The exclusive upper bound.</param>
    /// <returns>A random unsigned integer in the range [0, max).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new uint Get(uint max)
    {
        if (max == 0)
            return 0;

        // Fast path for power of 2
        if ((max & (max - 1)) == 0)
            return Get() & (max - 1);

        // Avoid modulo bias by rejecting values in the unfair region
        uint threshold = RandMax - (RandMax % max);
        uint result;
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong Get(ulong max)
    {
        if (max == 0)
            return 0;

        // Fast path for small values that fit in uint
        if (max <= uint.MaxValue)
            return Get((uint)max);

        // Optimize for powers of 2
        if ((max & (max - 1)) == 0)
            return Get64() & (max - 1);

        // Use rejection sampling to avoid modulo bias
        ulong threshold = ulong.MaxValue - (ulong.MaxValue % max);
        ulong result;
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long Get(long max)
    {
        if (max <= 0)
            return 0;

        // Ensure positive result
        ulong result = Get((ulong)max);
        return (long)result;
    }

    /// <summary>
    /// Returns a random integer in the range [min, max).
    /// </summary>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The exclusive upper bound.</param>
    /// <returns>A random integer in the range [min, max).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Get(int min, int max)
    {
        if (min >= max)
            return min;

        // Handle potential overflow when calculating range
        uint range = (uint)(max - min);
        return min + (int)Get(range);
    }

    /// <summary>
    /// Returns a random unsigned integer in the range [min, max).
    /// </summary>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The exclusive upper bound.</param>
    /// <returns>A random unsigned integer in the range [min, max).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new uint Get(uint min, uint max)
    {
        if (min >= max)
            return min;

        uint range = max - min;
        return min + Get(range);
    }

    /// <summary>
    /// Returns a random unsigned long integer in the range [min, max).
    /// </summary>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The exclusive upper bound.</param>
    /// <returns>A random unsigned long integer in the range [min, max).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong Get(ulong min, ulong max)
    {
        if (min >= max)
            return min;

        ulong range = max - min;
        return min + Get(range);
    }

    /// <summary>
    /// Returns a random signed long integer in the range [min, max).
    /// Fixed signature to use long for both parameters.
    /// </summary>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The exclusive upper bound.</param>
    /// <returns>A random signed long integer in the range [min, max).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long Get(long min, long max)
    {
        if (min >= max)
            return min;

        // Handle negative range carefully
        ulong range = (ulong)(max - min);
        return min + (long)Get(range);
    }

    /// <summary>
    /// Returns a random floating-point Number in the range [0, max).
    /// </summary>
    /// <param name="max">The exclusive upper bound.</param>
    /// <returns>A random floating-point Number in the range [0, max).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Get(float max)
    {
        if (max <= 0)
            return 0;

        return GetFloat() * max;
    }

    /// <summary>
    /// Returns a random double-precision floating-point Number in the range [0, max).
    /// </summary>
    /// <param name="max">The exclusive upper bound.</param>
    /// <returns>A random double-precision floating-point Number in the range [0, max).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double Get(double max)
    {
        if (max <= 0)
            return 0;

        return GetDouble() * max;
    }

    /// <summary>
    /// Returns a random floating-point Number in the range [min, max).
    /// </summary>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The exclusive upper bound.</param>
    /// <returns>A random floating-point Number in the range [min, max).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Get(float min, float max)
    {
        if (min >= max)
            return min;

        return min + GetFloat() * (max - min);
    }

    /// <summary>
    /// Returns a random double-precision floating-point Number in the range [min, max).
    /// </summary>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The exclusive upper bound.</param>
    /// <returns>A random double-precision floating-point Number in the range [min, max).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double Get(double min, double max)
    {
        if (min >= max)
            return min;

        return min + GetDouble() * (max - min);
    }

    /// <summary>
    /// Returns a random boolean with the specified probability of being true.
    /// </summary>
    /// <param name="probability">The probability of returning true (0.0 to 1.0).</param>
    /// <returns>A random boolean.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool GetBool(double probability = 0.5)
    {
        if (probability <= 0.0)
            return false;
        if (probability >= 1.0)
            return true;

        return GetDouble() < probability;
    }

    /// <summary>
    /// Returns a random floating-point Number in the range [0.0, 1.0).
    /// </summary>
    /// <remarks>
    /// This implementation ensures uniform distribution across the entire range
    /// and avoids common precision issues in floating-point random generation.
    /// </remarks>
    /// <returns>A random float in the range [0.0, 1.0).</returns>
    // Use 24 bits (mantissa size for float) to ensure uniform distribution
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new float GetFloat() => (Get() >> 8) * (1.0f / 16777216.0f);

    /// <summary>
    /// Returns a random double-precision floating-point Number in the range [0.0, 1.0).
    /// </summary>
    /// <remarks>
    /// This implementation ensures uniform distribution across the entire range
    /// and uses the full 53-bit precision available in a double.
    /// </remarks>
    /// <returns>A random double in the range [0.0, 1.0).</returns>
    // Use all 53 bits of precision available in a double
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new double GetDouble() => (Get64() >> 11) * (1.0 / 9007199254740992.0);

    /// <summary>
    /// Fills the specified buffer with random bytes.
    /// </summary>
    /// <param name="buffer">The buffer to fill with random bytes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new void NextBytes(Span<byte> buffer) => base.NextBytes(buffer);
}
