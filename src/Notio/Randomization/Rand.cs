using System;

namespace Notio.Randomization;

/// <summary>
/// A class that provides methods for generating random numbers.
/// </summary>
/// <remarks>
/// Initializes a Rand instance with a given seed value.
/// </remarks>
/// <param name="seed">The seed value to initialize the random number generator.</param>
public sealed class Rand(uint seed) : RandMwc(seed)
{
    /// <summary>
    /// Returns a random floating-point number between 0.0 and 1.0.
    /// </summary>
    /// <returns>A random floating-point number between 0.0 and 1.0.</returns>
    public float GetFloat() => BitConverter.UInt32BitsToSingle(Get() & 0x7fffff | 0x3f800000) - 1.0f;

    /// <summary>
    /// Returns a random double-precision floating-point number between 0.0 and 1.0.
    /// </summary>
    /// <returns>A random double-precision floating-point number between 0.0 and 1.0.</returns>
    public double GetDouble() => BitConverter.UInt64BitsToDouble(Get64() & 0xfffffffffffff | 0x3ff0000000000000) - 1.0;

    /// <summary>
    /// Returns a random number from 0 to max (excluding max).
    /// </summary>
    /// <param name="max">The upper limit of the random value.</param>
    /// <returns>A random number from 0 to max - 1.</returns>
    public uint Get(uint max) => max == 0 ? 0 : Get() % max;

    /// <summary>
    /// Returns a random integer from 0 to max (excluding max).
    /// </summary>
    /// <param name="max">The upper limit of the random value.</param>
    /// <returns>A random integer from 0 to max - 1.</returns>
    public int Get(int max) => max <= 0 ? 0 : (int)(Get() & 0x7fffffff) % max;

    /// <summary>
    /// Returns a random unsigned integer from 0 to max (excluding max).
    /// </summary>
    /// <param name="max">The upper limit of the random value.</param>
    /// <returns>A random unsigned integer from 0 to max - 1.</returns>
    public ulong Get(ulong max) => max == 0 ? 0 : Get64() % max;

    /// <summary>
    /// Returns a random signed integer from 0 to max (excluding max).
    /// </summary>
    /// <param name="max">The upper limit of the random value.</param>
    /// <returns>A random signed integer from 0 to max - 1.</returns>
    public long Get(long max) => max == 0 ? 0 : (long)(Get64() & 0x7fffffffffffffffUL) % max;

    /// <summary>
    /// Returns a random double-precision floating-point number from 0.0 to max (excluding max).
    /// </summary>
    /// <param name="max">The upper limit of the random value.</param>
    /// <returns>A random double-precision floating-point number from 0.0 to max.</returns>
    public double Get(double max) => max <= 0.0f ? 0.0f : GetDouble() * max;

    /// <summary>
    /// Returns a random number from min to max (inclusive).
    /// </summary>
    /// <param name="min">The lower limit of the random value.</param>
    /// <param name="max">The upper limit of the random value.</param>
    /// <returns>A random number from min to max.</returns>
    public uint Get(uint min, uint max)
    {
        if (max < min)
            return max;

        uint range = max - min + 1;
        return range == 0 ? Get() : Get(range) + min;
    }

    /// <summary>
    /// Returns a random floating-point number from 0.0 to max (excluding max).
    /// </summary>
    /// <param name="max">The upper limit of the random value.</param>
    /// <returns>A random floating-point number from 0.0 to max.</returns>
    public float Get(float max)
        => max <= 0.0f ? 0.0f : GetFloat() * max;

    /// <summary>
    /// Returns a random integer from min to max (inclusive).
    /// </summary>
    /// <param name="min">The lower limit of the random value.</param>
    /// <param name="max">The upper limit of the random value.</param>
    /// <returns>A random integer from min to max.</returns>
    public int Get(int min, int max)
    {
        if (max < min)
            return max;

        uint range = (uint)max - (uint)min + 1;
        return (int)(range == 0 ? Get() : Get(range) + min);
    }

    /// <summary>
    /// Returns a random unsigned integer from min to max (inclusive).
    /// </summary>
    /// <param name="min">The lower limit of the random value.</param>
    /// <param name="max">The upper limit of the random value.</param>
    /// <returns>A random unsigned integer from min to max.</returns>
    public ulong Get(ulong min, ulong max)
    {
        if (max < min)
            return max;

        ulong range = max - min + 1;
        return range == 0 ? Get64() : Get(range) + min;
    }

    /// <summary>
    /// Returns a random signed integer from min to max (inclusive).
    /// </summary>
    /// <param name="min">The lower limit of the random value.</param>
    /// <param name="max">The upper limit of the random value.</param>
    /// <returns>A random signed integer from min to max.</returns>
    public long Get(ulong min, long max)    // ulong min <-- same as in the client
    {
        if (max < (long)min)
            return max;

        ulong range = (ulong)max - min + 1;
        return (long)(range == 0 ? Get() : Get(range) + min);
    }

    /// <summary>
    /// Returns a random floating-point number from min to max (inclusive).
    /// </summary>
    /// <param name="min">The lower limit of the random value.</param>
    /// <param name="max">The upper limit of the random value.</param>
    /// <returns>A random floating-point number from min to max.</returns>
    public float Get(float min, float max) => max < min ? max : GetFloat() * (max - min) + min;

    /// <summary>
    /// Returns a random double-precision floating-point number from min to max (inclusive).
    /// </summary>
    /// <param name="min">The lower limit of the random value.</param>
    /// <param name="max">The upper limit of the random value.</param>
    /// <returns>A random double-precision floating-point number from min to max.</returns>
    public double Get(double min, double max) => max < min ? max : GetDouble() * (max - min) + min;
}
