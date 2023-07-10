namespace Nalix.Cryptography.Internal;

internal static partial class BitwiseUtils
{
    /// <summary>
    /// Creates a fixed-length key from an arbitrary-length input span.
    /// If the input is shorter than the desired length, the result is zero-padded.
    /// If the input is longer, it is truncated.
    /// </summary>
    /// <param name="input">The input key material. Can be of any length.</param>
    /// <param name="length">The required length in bytes for the resulting key.</param>
    /// <returns>A new byte array of the exact specified length.</returns>
    /// <exception cref="System.ArgumentOutOfRangeException">
    /// Thrown if <paramref name="length"/> is negative.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Byte[] FixedSize(System.ReadOnlySpan<System.Byte> input, System.Int32 length = 16)
    {
        if (length < 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(length), "Length must be non-negative.");
        }

        System.Int32 bytesToCopy = System.Math.Min(input.Length, length);
        System.Byte[] result = new System.Byte[length];
        input[..bytesToCopy].CopyTo(result);

        return result;
    }

    /// <summary>
    /// Compares two byte spans in a fixed-time manner to prevent timing attacks.
    /// </summary>
    /// <param name="left">The first byte span to compare.</param>
    /// <param name="right">The second byte span to compare.</param>
    /// <returns>True if the byte spans are equal; otherwise, false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.NoOptimization)]
    public static System.Boolean FixedTimeEquals(
        System.ReadOnlySpan<System.Byte> left,
        System.ReadOnlySpan<System.Byte> right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        System.Int32 result = 0;

        for (System.Int32 i = 0; i < left.Length; i++)
        {
            result |= left[i] ^ right[i];
        }

        return result == 0;
    }

    /// <summary>
    /// Increments a counter value stored in a byte array.
    /// </summary>
    /// <param name="counter">The counter to increment.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void IncrementCounter(System.Span<System.Byte> counter)
    {
        for (System.Int32 i = 0; i < counter.Length; i++)
        {
            if (++counter[i] != 0)
            {
                break;
            }
        }
    }
}