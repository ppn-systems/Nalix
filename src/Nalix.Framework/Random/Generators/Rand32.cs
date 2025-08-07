// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Framework.Random.Generators;

/// <summary>
/// A .NET-style wrapper around a high-performance random number generator.
/// </summary>
/// <remarks>
/// This class wraps a low-level random number generator (SeededRandom)
/// and exposes a familiar interface similar to <see cref="System.Random"/>.
/// </remarks>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerStepThrough]
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class Rand32
{
    #region Constants

    /// <summary>
    /// The maximum possible generated integer value.
    /// </summary>
    public const System.Int32 RandMax = 0x7FFFFFFF;

    /// <summary>
    /// Inverse of RandMax as a double for faster calculations.
    /// </summary>
    public const System.Double InvRandMax = 1.0 / RandMax;

    #endregion Constants

    #region Fields

    /// <summary>
    /// Current seed value for the random number generator.
    /// </summary>
    private System.Int32 _seed;

    /// <summary>
    /// Random number generator instance.
    /// </summary>
    private readonly SeededRandom _rand;

    private System.String DebuggerDisplay => $"Rand32[Seed={_seed}]";

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="Rand32"/> class with the default seed value of 0.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "<Pending>")]
    public Rand32(System.Int32 seed)
    {
        _seed = seed;
        _rand = new((System.UInt32)seed);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Rand32"/> class with the default seed value of 0.
    /// </summary>
    public Rand32() : this(0)
    {
    }

    #endregion Constructors

    #region APIs

    /// <summary>
    /// Resets the seed for the random number generator.
    /// </summary>
    /// <param name="seed">The new seed value.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void Seed(System.Int32 seed)
    {
        _seed = seed;
        _rand.SetSeed((System.UInt32)seed);
    }

    /// <summary>
    /// Gets the current seed value.
    /// </summary>
    /// <returns>The current seed value.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Int32 GetSeed() => _seed;

    /// <summary>
    /// Generates a random integer in the range [0, RandMax].
    /// </summary>
    /// <returns>A random integer.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Int32 Next() => (System.Int32)(_rand.Get() & RandMax);

    /// <summary>
    /// Generates a random integer in the range [0, max).
    /// </summary>
    /// <param name="max">The exclusive upper bound.</param>
    /// <returns>A random integer.</returns>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown when max is less than or equal to 0.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Int32 Next(System.Int32 max)
    {
        if (max <= 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(max), max, "Max must be positive.");
        }

        // Fast path for power of 2
        if ((max & (max - 1)) == 0)
        {
            return (System.Int32)(((_rand.Get() & RandMax) * max) >> 31);
        }

        // Avoid modulo bias by rejecting values in the unfair region
        System.UInt32 threshold = (System.UInt32)((RandMax - (RandMax % max)) & RandMax);
        System.UInt32 result;
        do
        {
            result = _rand.Get() & RandMax;
        } while (result >= threshold);

        return (System.Int32)(result % max);
    }

    /// <summary>
    /// Generates a random integer in the range [min, max).
    /// </summary>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The exclusive upper bound.</param>
    /// <returns>A random integer.</returns>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown when min is greater than or equal to max.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Int32 Next(System.Int32 min, System.Int32 max)
    {
        if (min >= max)
        {
            return min == max ? min : throw new System.ArgumentOutOfRangeException(nameof(min), min, $"Min ({min}) must be less than max ({max}).");
        }

        System.Int64 range = (System.Int64)max - min;
        if (range <= System.Int32.MaxValue)
        {
            return min + this.Next((System.Int32)range);
        }

        // Handle large ranges that exceed int.MaxValue
        return min + (System.Int32)(this.NextDouble() * range);
    }

    /// <summary>
    /// Generates a random floating-point value in the range [0.0f, 1.0f].
    /// </summary>
    /// <returns>A random float.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Single NextFloat() => _rand.GetFloat();

    /// <summary>
    /// Generates a random floating-point value in the range [0.0f, max).
    /// </summary>
    /// <param name="max">The exclusive upper bound.</param>
    /// <returns>A random float.</returns>
    /// <exception cref="System.ArgumentException">Thrown when max is NaN or Infinity.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Single NextFloat(System.Single max)
    {
        if (System.Single.IsNaN(max) || System.Single.IsInfinity(max))
        {
            throw new System.ArgumentException("Max must be a finite number", nameof(max));
        }

        return this.NextFloat() * max;
    }

    /// <summary>
    /// Generates a random floating-point value in the range [min, max).
    /// </summary>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The exclusive upper bound.</param>
    /// <returns>A random float.</returns>
    /// <exception cref="System.ArgumentException">Thrown when min or max is NaN or Infinity, or when min is greater than or equal to max.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Single NextFloat(System.Single min, System.Single max)
    {
        if (System.Single.IsNaN(min) || System.Single.IsInfinity(min))
        {
            throw new System.ArgumentException("Min must be a finite number", nameof(min));
        }

        if (System.Single.IsNaN(max) || System.Single.IsInfinity(max))
        {
            throw new System.ArgumentException("Max must be a finite number", nameof(max));
        }

        if (min >= max)
        {
            throw new System.ArgumentException($"Min ({min}) must be less than max ({max}).");
        }

        return min + (this.NextFloat() * (max - min));
    }

    /// <summary>
    /// Generates a random double-precision floating-point value in the range [0.0, 1.0].
    /// </summary>
    /// <returns>A random double.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Double NextDouble() => _rand.GetDouble();

    /// <summary>
    /// Generates a random double-precision floating-point value in the range [0.0, max).
    /// </summary>
    /// <param name="max">The exclusive upper bound.</param>
    /// <returns>A random double.</returns>
    /// <exception cref="System.ArgumentException">Thrown when max is NaN or Infinity.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Double NextDouble(System.Double max)
    {
        if (System.Double.IsNaN(max) || System.Double.IsInfinity(max))
        {
            throw new System.ArgumentException("Max must be a finite number", nameof(max));
        }

        return this.NextDouble() * max;
    }

    /// <summary>
    /// Generates a random double-precision floating-point value in the range [min, max).
    /// </summary>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The exclusive upper bound.</param>
    /// <returns>A random double.</returns>
    /// <exception cref="System.ArgumentException">Thrown when min or max is NaN or Infinity, or when min is greater than or equal to max.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Double NextDouble(System.Double min, System.Double max)
    {
        if (System.Double.IsNaN(min) || System.Double.IsInfinity(min))
        {
            throw new System.ArgumentException("Min must be a finite number", nameof(min));
        }

        if (System.Double.IsNaN(max) || System.Double.IsInfinity(max))
        {
            throw new System.ArgumentException("Max must be a finite number", nameof(max));
        }

        if (min >= max)
        {
            throw new System.ArgumentException($"Min ({min}) must be less than max ({max}).");
        }

        return min + (this.NextDouble() * (max - min));
    }

    /// <summary>
    /// Performs a random check with a given percentage probability.
    /// </summary>
    /// <param name="pct">The percentage probability (0-100).</param>
    /// <returns>True if the random check passed based on the specified percentage.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Boolean NextPct(System.Int32 pct) => pct > 0 && (pct >= 100 || this.Next(100) < pct);

    /// <summary>
    /// Performs a random check with a given probability.
    /// </summary>
    /// <param name="probability">The probability (0.0-1.0).</param>
    /// <returns>True if the random check passed based on the specified probability.</returns>
    /// <exception cref="System.ArgumentException">Thrown when probability is NaN or Infinity.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Boolean NextProbability(System.Double probability)
    {
        if (System.Double.IsNaN(probability) || System.Double.IsInfinity(probability))
        {
            throw new System.ArgumentException("Probability must be a finite number", nameof(probability));
        }

        return probability > 0.0 && (probability >= 1.0 || this.NextDouble() < probability);
    }

    /// <summary>
    /// Randomly shuffles a list using the Fisher-Yates algorithm.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <param name="list">The list to shuffle.</param>
    public void ShuffleList<T>(System.Collections.Generic.IList<T> list)
    {
        System.ArgumentNullException.ThrowIfNull(list);

        System.Int32 n = list.Count;
        while (n > 1)
        {
            n--;
            System.Int32 k = this.Next(n + 1);
            (list[k], list[n]) = (list[n], list[k]);
        }
    }

    /// <summary>
    /// Randomly shuffles a span using the Fisher-Yates algorithm.
    /// </summary>
    /// <typeparam name="T">The type of elements in the span.</typeparam>
    /// <param name="span">The span to shuffle.</param>
    public void ShuffleSpan<T>(System.Span<T> span)
    {
        System.Int32 n = span.Length;
        while (n > 1)
        {
            n--;
            System.Int32 k = this.Next(n + 1);
            (span[k], span[n]) = (span[n], span[k]);
        }
    }

    /// <summary>
    /// Returns a random item from the list.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <param name="list">The list to pick from.</param>
    /// <returns>A random item from the list.</returns>
    /// <exception cref="System.ArgumentException">Thrown when the list is empty.</exception>
    public T Choose<T>(System.Collections.Generic.IList<T> list)
    {
        return list == null || list.Count == 0
            ? throw new System.ArgumentException("Cannot choose from an empty list", nameof(list))
            : list[this.Next(list.Count)];
    }

    /// <summary>
    /// Returns a random item from the span.
    /// </summary>
    /// <typeparam name="T">The type of elements in the span.</typeparam>
    /// <param name="span">The span to pick from.</param>
    /// <returns>A random item from the span.</returns>
    /// <exception cref="System.ArgumentException">Thrown when the span is empty.</exception>
    public T Choose<T>(System.ReadOnlySpan<T> span)
        => span.IsEmpty ? throw new System.ArgumentException(
            "Cannot choose from an empty span", nameof(span)) : span[this.Next(span.Length)];

    /// <summary>
    /// Fills the specified buffer with random bytes.
    /// </summary>
    /// <param name="buffer">The buffer to fill.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void NextBytes(System.Span<System.Byte> buffer) => _rand.NextBytes(buffer);

    /// <summary>
    /// Returns a string representation of the random number generator state.
    /// </summary>
    /// <returns>A string representation of the RNG.</returns>
    public override System.String ToString() => $"Rand32[Seed={_seed}]";

    #endregion APIs
}