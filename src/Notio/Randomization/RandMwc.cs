using System;

namespace Notio.Randomization;

/// <summary>
/// A class that provides methods for generating random numbers using the Multiply-with-carry (MWC) algorithm.
/// </summary>
public abstract class RandMwc
{
    private ulong _seed;

    /// <summary>
    /// The maximum possible value that can be generated.
    /// </summary>
    public const uint RandMax = 0xffffffff;

    /// <summary>
    /// Initializes a RandMwc instance with a given seed value.
    /// </summary>
    /// <param name="seed">The seed value to initialize the random number generator.</param>
    protected RandMwc(uint seed)
        => SetSeed(seed == 0 ? (uint)DateTime.Now.Ticks : seed);

    /// <summary>
    /// Sets the seed value for the random number generator.
    /// </summary>
    /// <param name="seed">The new seed value.</param>
    public void SetSeed(uint seed) => _seed = (ulong)666 << 32 | seed;

    /// <summary>
    /// Returns a random number.
    /// </summary>
    /// <returns>A random number as a uint.</returns>
    public uint Get()
    {
        _seed = 698769069UL * (_seed & 0xffffffff) + (_seed >> 32);
        return (uint)_seed;
    }

    /// <summary>
    /// Returns a 64-bit random number.
    /// </summary>
    /// <returns>A random number as a ulong.</returns>
    protected ulong Get64() => (ulong)Get() << 32 | Get();

    /// <summary>
    /// Returns a string representation of the current seed value.
    /// </summary>
    /// <returns>A string representing the seed value.</returns>
    public override string ToString() => $"0x{_seed:X16}";
}
