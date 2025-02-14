using System.Collections.Generic;

namespace Notio.Randomization;

/// <summary>
/// A class that supports generating random numbers with various data types and ranges.
/// </summary>
public sealed class GRandom
{
    /// <summary>
    /// The maximum possible generated value.
    /// </summary>
    public const int RandMax = 0x7fffffff;

    private int _seed;           // Seed for the random number generator
    private readonly Rand _rand; // Random number generator instance

    /// <summary>
    /// Initializes with the default seed value of 0.
    /// </summary>
    public GRandom()
    {
        _seed = 0;
        _rand = new Rand(0);
    }

    /// <summary>
    /// Initializes with a user-provided seed value.
    /// </summary>
    public GRandom(int seed)
    {
        _seed = seed;
        _rand = new Rand((uint)seed);
    }

    /// <summary>
    /// Resets the seed for the random number generator.
    /// </summary>
    public void Seed(int seed)
    {
        _seed = seed;
        _rand.SetSeed((uint)seed);
    }

    /// <summary>
    /// Gets the current seed value.
    /// </summary>
    public int GetSeed() => _seed;

    /// <summary>
    /// Generates a random integer in the range [0, RandMax].
    /// </summary>
    public int Next() => Next(RandMax);

    /// <summary>
    /// Generates a random integer in the range [0, max).
    /// </summary>
    public int Next(int max) => Next(0, max);

    /// <summary>
    /// Generates a random integer in the range [min, max).
    /// </summary>
    public int Next(int min, int max)
    {
        if (min == max)
            return min;
        int range = max - min;
        return (int)(_rand.Get() & RandMax) % range + min;
    }

    /// <summary>
    /// Generates a random floating-point number in the range [0.0f, 1.0f].
    /// </summary>
    public float NextFloat() => _rand.GetFloat();

    /// <summary>
    /// Generates a random floating-point number in the range [0.0f, max).
    /// </summary>
    public float NextFloat(float max) => _rand.Get(0.0f, max);

    /// <summary>
    /// Generates a random floating-point number in the range [min, max).
    /// </summary>
    public float NextFloat(float min, float max) => _rand.Get(min, max);

    /// <summary>
    /// Generates a random double-precision floating-point number in the range [0.0, 1.0].
    /// </summary>
    public double NextDouble() => _rand.GetDouble();

    /// <summary>
    /// Generates a random double-precision floating-point number in the range [0.0, max).
    /// </summary>
    public double NextDouble(double max) => _rand.Get(0.0, max);

    /// <summary>
    /// Generates a random double-precision floating-point number in the range [min, max).
    /// </summary>
    public double NextDouble(double min, double max) => _rand.Get(min, max);

    /// <summary>
    /// Performs a random check with a given percentage probability.
    /// </summary>
    public bool NextPct(int pct) => Next(0, 100) < pct;

    /// <summary>
    /// Randomly shuffles a list.
    /// </summary>
    public void ShuffleList<T>(List<T> list)
    {
        if (list.Count > 1)
            for (int i = 0; i < list.Count; i++)
            {
                int j = Next(i, list.Count);
                (list[j], list[i]) = (list[i], list[j]);
            }
    }

    /// <summary>
    /// Returns a string representation of the random number generator state.
    /// </summary>
    public override string ToString() => _rand.ToString();
}
