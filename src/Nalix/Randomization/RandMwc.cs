using System;
using System.Runtime.CompilerServices;

namespace Nalix.Randomization;

/// <summary>
/// A high-performance implementation of the Multiply-with-carry (MWC) random Number generator algorithm.
/// </summary>
/// <remarks>
/// This implementation uses a 64-bit state value to produce 32-bit random numbers.
/// The generator has a period of approximately 2^63 and provides good statistical properties.
/// </remarks>
public abstract class RandMwc
{
    // Performance for the MWC algorithm
    private const ulong Multiplier = 698769069UL;

    private const ulong InitialCarry = 666UL;

    /// <summary>
    /// The maximum possible value that can be generated (2^32 - 1).
    /// </summary>
    public const uint RandMax = 0xFFFFFFFF;

    /// <summary>
    /// The inverse of RandMax as a double for faster floating-point conversions.
    /// </summary>
    protected const double InvRandMax = 1.0 / RandMax;

    /// <summary>
    /// The internal state of the generator, combining both the current value and carry.
    /// </summary>
    private ulong _state;

    /// <summary>
    /// Initializes a RandMwc instance with a given seed value.
    /// </summary>
    /// <param name="seed">The seed value to initialize the random Number generator. If 0, uses the current time.</param>
    protected RandMwc(uint seed)
    {
        // If seed is 0, use current time ticks as a seed
        if (seed == 0)
        {
            seed = (uint)(DateTime.UtcNow.Ticks & 0xFFFFFFFF);
            // Ensure seed is never 0 even if ticks lower bits are 0
            if (seed == 0) seed = 1;
        }

        SetSeed(seed);
    }

    /// <summary>
    /// Sets the seed value for the random Number generator.
    /// </summary>
    /// <param name="seed">The new seed value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetSeed(uint seed)
    {
        _state = (InitialCarry << 32) | seed;

        // Warm up the generator to avoid initial patterns
        for (int i = 0; i < 10; i++)
            Get();
    }

    /// <summary>
    /// Gets the current seed value.
    /// </summary>
    /// <returns>The current seed as a uint.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint GetSeed() => (uint)_state;

    /// <summary>
    /// Returns a random Number in the range [0, RandMax].
    /// </summary>
    /// <returns>A random Number as a uint.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint Get()
    {
        // MWC algorithm: state = (multiplier * (state & 0xFFFFFFFF) + (state >> 32))
        _state = Multiplier * (_state & 0xFFFFFFFF) + (_state >> 32);
        return (uint)_state;
    }

    /// <summary>
    /// Returns a random Number in the range [0, max).
    /// </summary>
    /// <param name="max">The exclusive upper bound.</param>
    /// <returns>A random Number in the range [0, max).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint Get(uint max)
    {
        if (max <= 1)
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
    /// Returns a random Number in the range [min, max).
    /// </summary>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The exclusive upper bound.</param>
    /// <returns>A random Number in the range [min, max).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint Get(uint min, uint max)
    {
        if (min >= max)
            return min;

        return min + Get(max - min);
    }

    /// <summary>
    /// Returns a 64-bit random Number.
    /// </summary>
    /// <returns>A random Number as a ulong.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected ulong Get64()
    {
        // Use each 32-bit generation individually for better statistical properties
        ulong hi = Get();
        ulong lo = Get();
        return (hi << 32) | lo;
    }

    /// <summary>
    /// Returns a random float in the range [0.0f, 1.0f).
    /// </summary>
    /// <returns>A random float.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected float GetFloat()
    {
        // Use only 24 bits for floating point precision (mantissa size for float)
        return (Get() >> 8) * (1.0f / 16777216.0f);
    }

    /// <summary>
    /// Returns a random double in the range [0.0, 1.0).
    /// </summary>
    /// <returns>A random double.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected double GetDouble()
    {
        // Use all 53 bits of precision available in a double
        return (Get64() >> 11) * (1.0 / 9007199254740992.0);
    }

    /// <summary>
    /// Returns a string representation of the current generator state.
    /// </summary>
    /// <returns>A string representing the generator state.</returns>
    public override string ToString() => $"RandMwc[state=0x{_state:X16}]";

    /// <summary>
    /// Fills the given buffer with random bytes.
    /// </summary>
    /// <param name="buffer">The buffer to fill with random bytes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void NextBytes(Span<byte> buffer)
    {
        int i = 0;

        // Process 4 bytes at a time when possible
        while (i <= buffer.Length - 4)
        {
            uint value = Get();
            buffer[i++] = (byte)value;
            buffer[i++] = (byte)(value >> 8);
            buffer[i++] = (byte)(value >> 16);
            buffer[i++] = (byte)(value >> 24);
        }

        // Handle remaining bytes
        if (i < buffer.Length)
        {
            uint value = Get();
            while (i < buffer.Length)
            {
                buffer[i++] = (byte)value;
                value >>= 8;
            }
        }
    }
}
