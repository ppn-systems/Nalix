using System.Numerics;

namespace Notio.Cryptography.Extensions;

/// <summary>
/// Provides extension methods for BigInteger operations with modular arithmetic.
/// </summary>
internal static class BigIntegerExtensions
{
    /// <summary>
    /// Adds two BigInteger values with modulo operation.
    /// </summary>
    /// <param name="a">First BigInteger value.</param>
    /// <param name="b">Second BigInteger value.</param>
    /// <param name="mod">Modulo value.</param>
    /// <returns>The result of (a + b) % mod.</returns>
    public static BigInteger ModAdd(this BigInteger a, BigInteger b, BigInteger mod)
    {
        a += b;
        if (a >= mod)
            a -= mod;
        else if (a < 0)
            a += mod;
        return a;
    }

    /// <summary>
    /// Subtracts one BigInteger value from another with modulo operation.
    /// </summary>
    /// <param name="a">First BigInteger value.</param>
    /// <param name="b">Second BigInteger value.</param>
    /// <param name="mod">Modulo value.</param>
    /// <returns>The result of (a - b) % mod.</returns>
    public static BigInteger ModSub(this BigInteger a, BigInteger b, BigInteger mod)
    {
        a -= b;
        if (a < 0)
            a += mod;
        else if (a >= mod)
            a -= mod;
        return a;
    }

    /// <summary>
    /// Multiplies two BigInteger values with modulo operation.
    /// </summary>
    /// <param name="a">First BigInteger value.</param>
    /// <param name="b">Second BigInteger value.</param>
    /// <param name="mod">Modulo value.</param>
    /// <returns>The result of (a * b) % mod.</returns>
    public static BigInteger MultiplyMod(this BigInteger a, BigInteger b, BigInteger mod)
        => a * b % mod;

    /// <summary>
    /// Performs a modulo operation and ensures the result is non-negative.
    /// </summary>
    /// <param name="num">The BigInteger value.</param>
    /// <param name="modulo">The modulo value.</param>
    /// <returns>The result of num % modulo, adjusted to be non-negative.</returns>
    public static BigInteger Mod(this BigInteger num, BigInteger modulo)
    {
        var result = num % modulo;
        return result < 0 ? result + modulo : result;
    }
}
