namespace Nalix.Extensions.Primitives;

/// <summary>
/// Provides extension methods for working with any enumeration marked with the <see cref="System.FlagsAttribute"/>.
/// </summary>
/// <remarks>
/// These methods enable easy manipulation of flag-based enumerations, such as checking, adding, or removing flags.
/// </remarks>
public static class EnumFlagsExtensions
{
    /// <summary>
    /// Determines whether the specified flag is set in the given enumeration value.
    /// </summary>
    /// <typeparam name="TEnum">The enumeration type, which must be decorated with <see cref="System.FlagsAttribute"/>.</typeparam>
    /// <param name="flags">The enumeration value to check.</param>
    /// <param name="flag">The flag to check for.</param>
    /// <returns><see langword="true"/> if the specified flag is set; otherwise, <see langword="false"/>.</returns>

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static bool HasFlag<TEnum>(this TEnum flags, TEnum flag)
        where TEnum : struct, System.Enum
        => (System.Convert.ToUInt64(flags) & System.Convert.ToUInt64(flag)) == System.Convert.ToUInt64(flag);

    /// <summary>
    /// Adds the specified flag to the given enumeration value.
    /// </summary>
    /// <typeparam name="TEnum">The enumeration type, which must be decorated with <see cref="System.FlagsAttribute"/>.</typeparam>
    /// <param name="flags">The enumeration value to modify.</param>
    /// <param name="flag">The flag to add.</param>
    /// <returns>A new enumeration value with the specified flag added.</returns>

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static TEnum AddFlag<TEnum>(this TEnum flags, TEnum flag)
        where TEnum : struct, System.Enum
        => (TEnum)System.Enum.ToObject(typeof(TEnum), System.Convert.ToUInt64(flags) | System.Convert.ToUInt64(flag));

    /// <summary>
    /// Removes the specified flag from the given enumeration value.
    /// </summary>
    /// <typeparam name="TEnum">The enumeration type, which must be decorated with <see cref="System.FlagsAttribute"/>.</typeparam>
    /// <param name="flags">The enumeration value to modify.</param>
    /// <param name="flag">The flag to remove.</param>
    /// <returns>A new enumeration value with the specified flag removed.</returns>

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static TEnum RemoveFlag<TEnum>(this TEnum flags, TEnum flag)
        where TEnum : struct, System.Enum
        => (TEnum)System.Enum.ToObject(typeof(TEnum), System.Convert.ToUInt64(flags) & ~System.Convert.ToUInt64(flag));

    /// <summary>
    /// Determines whether the enumeration value is set to none (zero).
    /// </summary>
    /// <typeparam name="TEnum">The enumeration type, which must be decorated with <see cref="System.FlagsAttribute"/>.</typeparam>
    /// <param name="flags">The enumeration value to check.</param>
    /// <returns><see langword="true"/> if no flags are set; otherwise, <see langword="false"/>.</returns>

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static bool IsNone<TEnum>(this TEnum flags)
        where TEnum : struct, System.Enum
        => System.Convert.ToUInt64(flags) == 0;

    /// <summary>
    /// Determines whether the enumeration value matches the specified required flags
    /// and does not contain any of the excluded flags.
    /// </summary>
    /// <typeparam name="TEnum">The enumeration type, which must be decorated with <see cref="System.FlagsAttribute"/>.</typeparam>
    /// <param name="flags">The enumeration value to check.</param>
    /// <param name="requiredFlags">The flags that must be present.</param>
    /// <param name="excludedFlags">The flags that must not be present.</param>
    /// <returns>
    /// <see langword="true"/> if the enumeration value contains all required flags
    /// and does not contain any excluded flags; otherwise, <see langword="false"/>.
    /// </returns>

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static bool Matches<TEnum>(this TEnum flags, TEnum requiredFlags, TEnum excludedFlags)
        where TEnum : struct, System.Enum
    {
        ulong f = System.Convert.ToUInt64(flags);
        return (f & System.Convert.ToUInt64(requiredFlags)) == System.Convert.ToUInt64(requiredFlags) &&
               (f & System.Convert.ToUInt64(excludedFlags)) == 0;
    }
}
