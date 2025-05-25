namespace Nalix.Extensions;

/// <summary>
/// Provides extension methods for convenience when working with <see cref="System.Reflection.PropertyInfo"/>.
/// </summary>
public static class PropertyInfoExtensions
{
    /// <summary>
    /// Converts a <see cref="System.Collections.Frozen.FrozenSet{T}"/> of <see cref="System.Reflection.PropertyInfo"/>
    /// into a <see cref="System.ReadOnlySpan{T}"/>.
    /// </summary>
    /// <param name="set">The frozen set containing property information.</param>
    /// <returns>A read-only span of <see cref="System.Reflection.PropertyInfo"/>.</returns>
    /// <remarks>
    /// This method is temporary since <see cref="System.Collections.Frozen.FrozenSet{T}"/> does not currently provide direct span access.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.ReadOnlySpan<System.Reflection.PropertyInfo> AsSpan(
        this System.Collections.Frozen.FrozenSet<System.Reflection.PropertyInfo> set)
    {
        System.Collections.Generic.List<System.Reflection.PropertyInfo> list = [.. set,];
        return System.Runtime.InteropServices.CollectionsMarshal.AsSpan(list);
    }
}
