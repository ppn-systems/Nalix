namespace Nalix.Extensions.Maths;

/// <summary>
/// Provides mathematical helper methods for array sizing.
/// </summary>
public static class MathExtensions
{
    private const int ArrayMaxLength = 0x7FFFFFC7;

    /// <summary>
    /// Returns the next capacity by doubling the current size,
    /// capped at the maximum allowed array length.
    /// </summary>
    /// <param name="size">Current array size.</param>
    /// <returns>Next array capacity.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static int NextCapacity(this int size)
    {
        int doubled = unchecked(size << 1);

        return (uint)doubled > ArrayMaxLength ? ArrayMaxLength : doubled;
    }
}
