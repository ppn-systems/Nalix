namespace Nalix.Cryptography.Internal;

internal static partial class BitwiseUtils
{
    public static System.Boolean SequenceEqual(
        this System.Byte[] a,
        System.ReadOnlySpan<System.Byte> b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        System.Int32 result = 0;
        for (System.Int32 i = 0; i < a.Length; i++)
        {
            result |= a[i] ^ b[i];
        }

        return result == 0;
    }

    public static System.Byte[] Reverse(this System.Byte[] input)
    {
        System.Byte[] reversed = new System.Byte[input.Length];
        for (System.Int32 i = 0; i < input.Length; i++)
        {
            reversed[i] = input[input.Length - 1 - i];
        }

        return reversed;
    }
}
