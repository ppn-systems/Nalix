namespace Notio.Cryptography.Utilities;

public static partial class BitwiseUtils
{
    /// <summary>
    /// Compares two byte spans in a fixed-time manner to prevent timing attacks.
    /// </summary>
    /// <param name="left">The first byte span to compare.</param>
    /// <param name="right">The second byte span to compare.</param>
    /// <returns>True if the byte spans are equal; otherwise, false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.NoOptimization)]
    public static bool FixedTimeEquals(System.ReadOnlySpan<byte> left, System.ReadOnlySpan<byte> right)
    {
        if (left.Length != right.Length) return false;

        int result = 0;

        for (int i = 0; i < left.Length; i++)
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
    public static void IncrementCounter(System.Span<byte> counter)
    {
        for (int i = 0; i < counter.Length; i++)
        {
            if (++counter[i] != 0) break;
        }
    }
}
