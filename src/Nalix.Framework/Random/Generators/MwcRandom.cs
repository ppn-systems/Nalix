// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Framework.Random.Generators;

/// <summary>
/// A high-performance implementation of the Multiply-with-carry (MWC) random number generator algorithm.
/// </summary>
/// <remarks>
/// This implementation uses a 64-bit state value to produce 32-bit random numbers.
/// The generator has a period of approximately 2^63 and provides good statistical properties.
/// </remarks>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerStepThrough]
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
public abstract class MwcRandom
{
    #region Fields

    private System.String DebuggerDisplay => $"MwcRandom[state=0x{_state:X16}]";

    // Performance for the MWC algorithm
    private const System.UInt64 Multiplier = 698769069UL;

    private const System.UInt64 InitialCarry = 666UL;

    /// <summary>
    /// The maximum possible value that can be generated (2^32 - 1).
    /// </summary>
    public const System.UInt32 RandMax = 0xFFFFFFFF;

    /// <summary>
    /// The inverse of RandMax as a double for faster floating-point conversions.
    /// </summary>
    protected const System.Double InvRandMax = 1.0 / RandMax;

    /// <summary>
    /// The internal state of the generator, combining both the current value and carry.
    /// </summary>
    /// <remarks>
    /// Not thread-safe. Each thread should use its own instance of MwcRandom or derived classes.
    /// </remarks>
    private System.UInt64 _state;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a MwcRandom instance with a given seed value.
    /// </summary>
    /// <param name="seed">The seed value to initialize the random number generator. If 0, uses the current time.</param>
    /// <remarks>
    /// Using seed value 0 will generate a time-based seed, which provides better randomness for different instances
    /// created at different times. For reproducible sequences, provide a non-zero seed value.
    /// </remarks>
    protected MwcRandom(System.UInt32 seed)
    {
        // If seed is 0, use current time ticks as a seed
        if (seed == 0)
        {
            seed = (System.UInt32)(System.DateTime.UtcNow.Ticks & 0xFFFFFFFF);
            // Ensure seed is never 0 even if ticks lower bits are 0
            if (seed == 0)
            {
                seed = 1;
            }
        }

        SetSeed(seed);
    }

    #endregion Constructors

    #region APIs

    /// <summary>
    /// Sets the seed value for the random number generator.
    /// </summary>
    /// <param name="seed">The new seed value.</param>
    /// <remarks>
    /// This method initializes the internal state and performs a warm-up to avoid initial patterns
    /// in the generated sequence. Not thread-safe - do not call concurrently from multiple threads.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void SetSeed(System.UInt32 seed)
    {
        _state = (InitialCarry << 32) | seed;

        // Warm up the generator to avoid initial patterns
        for (System.Int32 i = 0; i < 10; i++)
        {
            _ = Get();
        }
    }

    /// <summary>
    /// Gets the current seed value.
    /// </summary>
    /// <returns>The current seed as a uint.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.UInt32 GetSeed() => (System.UInt32)_state;

    /// <summary>
    /// Returns a random value in the range [0, RandMax].
    /// </summary>
    /// <returns>A random value as a uint.</returns>
    /// <remarks>
    /// Uses the Multiply-with-carry (MWC) algorithm for high-quality pseudo-random number generation.
    /// This method is not thread-safe - use separate instances per thread for concurrent access.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.UInt32 Get()
    {
        // MWC algorithm: state = (multiplier * (state & 0xFFFFFFFF) + (state >> 32))
        _state = (Multiplier * (_state & 0xFFFFFFFF)) + (_state >> 32);
        return (System.UInt32)_state;
    }

    /// <summary>
    /// Returns a random value in the range [0, max).
    /// </summary>
    /// <param name="max">The exclusive upper bound.</param>
    /// <returns>A random value in the range [0, max).</returns>
    /// <remarks>
    /// Returns 0 if max is 0 or 1. Optimized fast path for power-of-2 values.
    /// Uses rejection sampling to avoid modulo bias for uniform distribution.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.UInt32 Get(System.UInt32 max)
    {
        if (max <= 1)
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
    /// Returns a random value in the range [min, max).
    /// </summary>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The exclusive upper bound.</param>
    /// <returns>A random value in the range [min, max).</returns>
    /// <remarks>
    /// Returns min if min ≥ max. Delegates to Get(max - min) for uniform distribution.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.UInt32 Get(System.UInt32 min, System.UInt32 max) => min >= max ? min : min + Get(max - min);

    /// <summary>
    /// Returns a 64-bit random value.
    /// </summary>
    /// <returns>A random value as a ulong.</returns>
    /// <remarks>
    /// Generates two 32-bit values and combines them to produce a 64-bit result.
    /// This approach provides better statistical properties than simple concatenation.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    protected System.UInt64 Get64()
    {
        // Use each 32-bit generation individually for better statistical properties
        System.UInt64 hi = Get();
        System.UInt64 lo = Get();
        return (hi << 32) | lo;
    }

    /// <summary>
    /// Returns a random float in the range [0.0f, 1.0f).
    /// </summary>
    /// <returns>A random float.</returns>
    /// <remarks>
    /// Uses 24 bits of precision (matching the mantissa size of float) to ensure uniform distribution
    /// across the entire range without precision loss.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    protected System.Single GetFloat() =>
        // Use only 24 bits for floating point precision (mantissa size for float)
        (Get() >> 8) * (1.0f / 16777216.0f);

    /// <summary>
    /// Returns a random double in the range [0.0, 1.0).
    /// </summary>
    /// <returns>A random double.</returns>
    /// <remarks>
    /// Uses all 53 bits of precision available in a double's mantissa to ensure uniform distribution
    /// across the entire range with maximum precision.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    protected System.Double GetDouble() =>
        // Use all 53 bits of precision available in a double
        (Get64() >> 11) * (1.0 / 9007199254740992.0);

    /// <summary>
    /// Returns a string representation of the current generator state.
    /// </summary>
    /// <returns>A string representing the generator state.</returns>
    public override System.String ToString() => $"MwcRandom[state=0x{_state:X16}]";

    /// <summary>
    /// Fills the given buffer with random bytes.
    /// </summary>
    /// <param name="buffer">The buffer to fill with random bytes.</param>
    /// <remarks>
    /// Efficiently fills the buffer by processing 4 bytes at a time when possible,
    /// minimizing the number of random number generations required.
    /// </remarks>
    [System.Runtime.CompilerServices.SkipLocalsInit]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    protected void NextBytes(System.Span<System.Byte> buffer)
    {
        System.Int32 i = 0;

        // Process 4 bytes at a time when possible
        while (i <= buffer.Length - 4)
        {
            System.UInt32 value = Get();
            buffer[i++] = (System.Byte)value;
            buffer[i++] = (System.Byte)(value >> 8);
            buffer[i++] = (System.Byte)(value >> 16);
            buffer[i++] = (System.Byte)(value >> 24);
        }

        // Handle remaining bytes
        if (i < buffer.Length)
        {
            System.UInt32 value = Get();
            while (i < buffer.Length)
            {
                buffer[i++] = (System.Byte)value;
                value >>= 8;
            }
        }
    }

    #endregion APIs
}